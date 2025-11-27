using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

namespace ImageSegmentation_K_Means
{
    public class ImageSegmentation
    {
        private int width, height;
        private byte[] rawColor;  // RGB kép
        private byte[] rawGrayscale;  // Szürkeárnyalatos kép
        private List<int> pixCluster;  // Klaszterek
        private byte[] fileHeader;  // A fájl fejlécét tároljuk itt
        private int[] gsHistogram;  // Szürkeárnyalatos histogram

        public ImageSegmentation()
        {
            fileHeader = new byte[54];  // BMP fájl fejléc
            gsHistogram = new int[256]; // A szürkeárnyalatos histogram
            pixCluster = new List<int>(); // Pixelek klasztereinek listája
        }

        public int PixG(int x, int y)
        {
            return rawGrayscale[y * width + x];
        }

        public void SetPixCluster(int x, int y, int cluster)
        {
            pixCluster[y * width + x] = cluster;
        }

        public void LoadImageFromFile(string filename)
        {
            using (FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read))
            {
                // Olvassuk a BMP fejlécet
                fs.Seek(18, SeekOrigin.Begin);
                width = ReadInt(fs);  // A képméret (szélesség)
                height = ReadInt(fs); // A képméret (magasság)

                // Színek tárolása (RGB)
                rawColor = new byte[width * height * 3];  // 3 byte színadatok / pixel (RGB)
                fs.Seek(54, SeekOrigin.Begin);  // A színadatok kezdete
                fs.Read(rawColor, 0, rawColor.Length);

                rawGrayscale = new byte[width * height];
                for (int i = 0; i < width * height; i++)
                {
                    // Szürkeárnyalatos érték számítása
                    rawGrayscale[i] = (byte)((rawColor[i * 3] + rawColor[i * 3 + 1] + rawColor[i * 3 + 2]) / 3);
                }

                // Fejléc másolása (ha szükséges)
                fs.Seek(0, SeekOrigin.Begin);
                fs.Read(fileHeader, 0, 54); // Fejléc 54 byte másolása

                // Histogram létrehozása
                Array.Clear(gsHistogram, 0, gsHistogram.Length);
                for (int i = 0; i < width * height; i++)
                {
                    gsHistogram[rawGrayscale[i]]++;
                }

                // Klaszterek inicializálása
                for (int i = 0; i < width * height; i++)
                {
                    pixCluster.Add(0);  // Kezdetben mindegyik pixelhez 0 klaszter tartozik
                }
            }
        }

        public void SavePixClusterToFile(string filename)
        {
            // BMP fájl fejlécének előkészítése
            byte[] header = new byte[54];

            // "BM" fájl típus
            header[0] = (byte)'B';
            header[1] = (byte)'M';

            // Fájl mérete (54 byte fejléc + pixel adatok)
            int fileSize = 54 + (width * height * 3) + ((width * 3) % 4 == 0 ? 0 : (4 - (width * 3) % 4));  // padding
            header[2] = (byte)(fileSize & 0xFF);
            header[3] = (byte)((fileSize >> 8) & 0xFF);
            header[4] = (byte)((fileSize >> 16) & 0xFF);
            header[5] = (byte)((fileSize >> 24) & 0xFF);

            // Kép kezdő pozíciója a fájlban
            header[10] = 54;  // 54 byte után kezdődik a képadat

            // Header size (40 byte)
            header[14] = 40;

            // Szélesség és magasság
            header[18] = (byte)(width & 0xFF);
            header[19] = (byte)((width >> 8) & 0xFF);
            header[20] = (byte)((width >> 16) & 0xFF);
            header[21] = (byte)((width >> 24) & 0xFF);

            header[22] = (byte)(height & 0xFF);
            header[23] = (byte)((height >> 8) & 0xFF);
            header[24] = (byte)((height >> 16) & 0xFF);
            header[25] = (byte)((height >> 24) & 0xFF);

            // Színmélység (24 bit = 3 byte per pixel)
            header[28] = 24;

            // BMP fájlba írása
            using (FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write))
            {
                // 1. Fejléc írása
                fs.Write(header, 0, 54);

                int padding = (4 - (width * 3) % 4) % 4;  // Padding kiszámítása, hogy 4 byte-os sorokat kapjunk
                byte[] paddingBytes = new byte[padding];  // Padding byte-ok

                // 2. Kép pixelek írása
                for (int y = height - 1; y >= 0; y--)  // BMP fájlokban az Y tengelyt fordítva kell írni (fordított sorrend)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte[] data = new byte[3];

                        // Klaszterekhez tartozó színek hozzárendelése
                        switch (pixCluster[y * width + x])
                        {
                            case 0:
                                data[0] = 100; // Kék
                                data[1] = 100; // Zöld
                                data[2] = 255; // Piros
                                break;
                            case 1:
                                data[0] = 100; // Kék
                                data[1] = 255; // Zöld
                                data[2] = 100; // Piros
                                break;
                            case 2:
                                data[0] = 255; // Kék
                                data[1] = 100; // Zöld
                                data[2] = 100; // Piros
                                break;
                            case 3:
                                data[0] = 255; // Kék
                                data[1] = 255; // Zöld
                                data[2] = 100; // Piros
                                break;
                            case 4:
                                data[0] = 255; // Kék
                                data[1] = 100; // Zöld
                                data[2] = 255; // Piros
                                break;
                        }

                        // BGR színadatok írása
                        fs.Write(data, 0, 3);
                    }

                    // Padding byte-ok írása
                    fs.Write(paddingBytes, 0, padding);
                }
            }
        }

        private int ReadInt(FileStream fs)
        {
            byte[] buffer = new byte[4];
            fs.Read(buffer, 0, 4);
            return BitConverter.ToInt32(buffer, 0);
        }

        public void KMeansClustering(int k, int maxIterations)
        {
            Random rand = new Random();

            // Klaszterek középpontjainak (centroidjainak) inicializálása (véletlenszerű RGB értékek)
            int[] centroidsR = new int[k];
            int[] centroidsG = new int[k];
            int[] centroidsB = new int[k];

            // Kezdeti centroidok (véletlenszerűen választva RGB értékek)
            for (int i = 0; i < k; i++)
            {
                centroidsR[i] = rand.Next(256);
                centroidsG[i] = rand.Next(256);
                centroidsB[i] = rand.Next(256);
            }

            bool converged = false;
            int iteration = 0;
            while (iteration < maxIterations && !converged)
            {
                Console.WriteLine($"Iteration {iteration}:");
                for (int i = 0; i < k; i++)
                {
                    Console.WriteLine($"Centroid {i}: R={centroidsR[i]}, G={centroidsG[i]}, B={centroidsB[i]}");
                }
                iteration++;
                // Klaszter hozzárendelés
                int[] newCentroidsR = new int[k];
                int[] newCentroidsG = new int[k];
                int[] newCentroidsB = new int[k];
                int[] count = new int[k];  // Pixelek számának nyomon követése klaszterenként
                // Klaszterek hozzárendelése és középpontok frissítése
                for (int i = 0; i < width * height; i++)
                {
                    int minDistance = int.MaxValue;
                    int closestCentroid = 0;
                    // A pixel RGB komponensei közvetlenül az rawColor-ból
                    int pixelR = rawColor[i * 3 + 2];  // piros komponens
                    int pixelG = rawColor[i * 3 + 1];  // zöld komponens
                    int pixelB = rawColor[i * 3];      // kék komponens
                    // RGB alapján keresünk középpontot
                    for (int j = 0; j < k; j++)
                    {
                        int distance = Math.Abs(pixelR - centroidsR[j]) + Math.Abs(pixelG - centroidsG[j]) + Math.Abs(pixelB - centroidsB[j]);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closestCentroid = j;
                        }
                    }
                    // Hozzárendelés a legközelebbi klaszterhez
                    pixCluster[i] = closestCentroid;
                    // Középpontok frissítése
                    newCentroidsR[closestCentroid] += pixelR;
                    newCentroidsG[closestCentroid] += pixelG;
                    newCentroidsB[closestCentroid] += pixelB;
                    count[closestCentroid]++;
                }
                // Klaszterek középpontjainak frissítése
            }

        }       
    }

    internal class Program
    {
        static void Main(string[] args)
        {
            ImageSegmentation problem = new ImageSegmentation();
            problem.LoadImageFromFile("mug.bmp");
            problem.KMeansClustering(2, 100);
            problem.SavePixClusterToFile("output.bmp");

        }
    }
}
