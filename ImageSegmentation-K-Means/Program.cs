using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
namespace ImageSegmentation_K_Means
{
    public class ImageSegmentation
    {
        private static Random rng = new Random();
        private int width, height;
        private byte[] rawColor; // RGB kép
        private byte[] rawGrayscale; // Szürkeárnyalatos kép
        private List<int> pixCluster; // Klaszterek
        private byte[] fileHeader; // A fájl fejlécét tároljuk itt
        private int[] gsHistogram; // Szürkeárnyalatos histogram
        public ImageSegmentation()
        {
            fileHeader = new byte[54]; // BMP fájl fejléc
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
                width = ReadInt(fs); // A képméret (szélesség)
                height = ReadInt(fs); // A képméret (magasság)
                // Színek tárolása (RGB)
                rawColor = new byte[width * height * 3]; // 3 byte színadatok / pixel (RGB)
                fs.Seek(54, SeekOrigin.Begin); // A színadatok kezdete
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
                    pixCluster.Add(0); // Kezdetben mindegyik pixelhez 0 klaszter tartozik
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
            int fileSize = 54 + (width * height * 3) + ((width * 3) % 4 == 0 ? 0 : (4 - (width * 3) % 4)); // padding
            header[2] = (byte)(fileSize & 0xFF);
            header[3] = (byte)((fileSize >> 8) & 0xFF);
            header[4] = (byte)((fileSize >> 16) & 0xFF);
            header[5] = (byte)((fileSize >> 24) & 0xFF);
            // Kép kezdő pozíciója a fájlban
            header[10] = 54; // 54 byte után kezdődik a képadat
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
                int padding = (4 - (width * 3) % 4) % 4; // Padding kiszámítása, hogy 4 byte-os sorokat kapjunk
                byte[] paddingBytes = new byte[padding]; // Padding byte-ok
                // 2. Kép pixelek írása
                for (int y = height - 1; y >= 0; y--) // BMP fájlokban az Y tengelyt fordítva kell írni (fordított sorrend)
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
        private byte[][] InitializeCentroids(int k)
        {
            Random rand = new Random();
            byte[][] centroids = new byte[k][];
            // Véletlenszerű centroidok kiválasztása
            for (int i = 0; i < k; i++)
            {
                centroids[i] = new byte[3];
                int randomPixelIndex = rand.Next(width * height);
                centroids[i][0] = rawColor[randomPixelIndex * 3 + 2]; // R
                centroids[i][1] = rawColor[randomPixelIndex * 3 + 1]; // G
                centroids[i][2] = rawColor[randomPixelIndex * 3]; // B
            }
            return centroids;
        }
        private int CalculateLuminance(int r, int g, int b)
        {
            // Luminance (fényerősség) számítása
            return (int)(0.2989 * r + 0.5870 * g + 0.1140 * b);
        }
        private int CalculateDistance(int r, int g, int b, byte[][] centroids)
        {
            int minDistance = int.MaxValue;
            int closestCentroid = 0;
            for (int i = 0; i < centroids.Length; i++)
            {
                int centroidR = centroids[i][0];
                int centroidG = centroids[i][1];
                int centroidB = centroids[i][2];
                // Euclidean távolság számítása
                int distance = (int)Math.Sqrt(
Math.Pow(r - centroidR, 2) +
Math.Pow(g - centroidG, 2) +
Math.Pow(b - centroidB, 2)
);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestCentroid = i;
                }
            }
            return closestCentroid;
        }
        private void AssignClusters(byte[][] centroids)
        {
            List<int>[] clusters = new List<int>[centroids.Length];
            for (int i = 0; i < centroids.Length; i++)
            {
                clusters[i] = new List<int>(); // Üres lista minden klaszterhez
            }
            // Klaszterek hozzárendelése a fényerősség alapján
            for (int i = 0; i < width * height; i++)
            {
                int pixelR = rawColor[i * 3 + 2];
                int pixelG = rawColor[i * 3 + 1];
                int pixelB = rawColor[i * 3];
                // Számoljuk ki a fényerősséget a pixel RGB értékeiből
                int pixelLuminance = CalculateLuminance(pixelR, pixelG, pixelB);
                int closestCentroid = -1;
                int minDistance = int.MaxValue;
                // Keresés a legközelebbi centroidra
                for (int j = 0; j < centroids.Length; j++)
                {
                    // Centroid fényerősségét (luminance) számoljuk
                    int centroidLuminance = CalculateLuminance(centroids[j][0], centroids[j][1], centroids[j][2]);
                    // Luminance alapú távolság számítása
                    int distance = Math.Abs(pixelLuminance - centroidLuminance);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        closestCentroid = j;
                    }
                }
                // Hozzárendelés a legközelebbi klaszterhez
                pixCluster[i] = closestCentroid;
                // Klaszterhez való hozzáadás
                clusters[closestCentroid].Add(i);
            }
        }
        private bool UpdateCentroids(byte[][] centroids)
        {
            bool converged = true;
            // Klaszterek újraszámítása
            List<int>[] clusters = new List<int>[centroids.Length];
            for (int i = 0; i < centroids.Length; i++)
            {
                clusters[i] = new List<int>(); // Üres lista minden klaszterhez
            }
            // Klaszterek frissítése
            for (int i = 0; i < width * height; i++)
            {
                int clusterIndex = pixCluster[i]; // Klaszter, amelyhez a pixel tartozik
                clusters[clusterIndex].Add(i); // Pixelt hozzáadjuk a megfelelő klaszterhez
            }
            // Frissítjük a centroidokat
            for (int i = 0; i < centroids.Length; i++)
            {
                int clusterSize = clusters[i].Count;
                if (clusterSize > 0)
                {
                    int totalR = 0, totalG = 0, totalB = 0;
                    // Átlagoljuk a klaszterekhez tartozó pixelek színértékeit
                    foreach (int pixelIndex in clusters[i])
                    {
                        totalR += rawColor[pixelIndex * 3 + 2];
                        totalG += rawColor[pixelIndex * 3 + 1];
                        totalB += rawColor[pixelIndex * 3];
                    }
                    // Új RGB értékek a centroidhoz
                    byte newR = (byte)(totalR / clusterSize);
                    byte newG = (byte)(totalG / clusterSize);
                    byte newB = (byte)(totalB / clusterSize);
                    // Ha a centroid színe változott, akkor nem konvergáltunk
                    if (centroids[i][0] != newR || centroids[i][1] != newG || centroids[i][2] != newB)
                    {
                        converged = false;
                    }
                    // Frissítjük a centroid színét
                    centroids[i][0] = newR;
                    centroids[i][1] = newG;
                    centroids[i][2] = newB;
                }
            }
            return converged;
        }
        public void KMeansClustering(int k, int maxIterations)
        {
            // Centroidok inicializálása
            byte[][] centroids = InitializeCentroids(k);
            bool converged = false;
            int iteration = 0;
            while (iteration < maxIterations && !converged)
            {
                iteration++;
                // Klaszterek hozzárendelése
                AssignClusters(centroids);
                // Centroidok frissítése
                converged = UpdateCentroids(centroids);
                // Itt írjuk ki a centroidokat minden iteráció után
                Console.WriteLine($"--- Iteration {iteration} ---");
                for (int i = 0; i < centroids.Length; i++)
                {
                    Console.WriteLine($"Centroid {i}: R={centroids[i][0]}, G={centroids[i][1]}, B={centroids[i][2]}");
                }
                Console.WriteLine($"Iteration {iteration} completed.");
            }
            Console.WriteLine("K-means clustering completed.");
        }
    }
    internal class Program
    {
        static void Main(string[] args)
        {
            ImageSegmentation problemCockatiel = new ImageSegmentation();
            problemCockatiel.LoadImageFromFile("cockatiel.bmp");
            problemCockatiel.KMeansClustering(3, 10);
            problemCockatiel.SavePixClusterToFile("outputcockatiel.bmp");

            ImageSegmentation problemMug = new ImageSegmentation();
            problemMug.LoadImageFromFile("mug.bmp");
            problemMug.KMeansClustering(2, 2);
            problemMug.SavePixClusterToFile("outputmug.bmp");
        }
    }
}