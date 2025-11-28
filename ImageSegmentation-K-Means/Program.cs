using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using static System.Runtime.InteropServices.JavaScript.JSType;
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

        private int bitsPerPixel;
        private int bytesPerPixel;
        private int stride;

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
                fs.Read(fileHeader, 0, 54);

                width = BitConverter.ToInt32(fileHeader, 18);
                height = BitConverter.ToInt32(fileHeader, 22);
                bitsPerPixel = BitConverter.ToInt16(fileHeader, 28);
                int offset = BitConverter.ToInt32(fileHeader, 10);

                bytesPerPixel = bitsPerPixel / 8;           // 3 vagy 4
                stride = (width * bytesPerPixel + 3) & ~3;   // 4 byte-ra igazítva

                // Teljes képadat beolvasása (paddinggel együtt)
                fs.Seek(offset, SeekOrigin.Begin);
                byte[] bmpData = new byte[stride * height];
                fs.Read(bmpData, 0, bmpData.Length);

                // Átalakítjuk BGR/BGRA → RGB tömbbe (felülről lefelé sorrendben)
                rawColor = new byte[width * height * 3];
                rawGrayscale = new byte[width * height];
                pixCluster.Clear();

                for (int y = 0; y < height; y++)
                {
                    int srcRow = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int src = srcRow + x * bytesPerPixel;
                        int dst = (y * width + x) * 3;

                        byte b = bmpData[src];
                        byte g = bmpData[src + 1];
                        byte r = bmpData[src + 2];

                        rawColor[dst] = r;
                        rawColor[dst + 1] = g;
                        rawColor[dst + 2] = b;

                        rawGrayscale[y * width + x] = (byte)((r + g + b) / 3);
                        pixCluster.Add(0);
                    }
                }

                // Histogram
                Array.Clear(gsHistogram, 0, 256);
                foreach (byte v in rawGrayscale) gsHistogram[v]++;
            }
        }

        public void SavePixClusterToFile(string filename)
        {
            int padding = (4 - (width * 3) % 4) % 4;
            byte[] padBytes = new byte[padding];

            using (FileStream fs = new FileStream(filename, FileMode.Create))
            {
                // 24-bit BMP fejléc (mindig 24-bitként mentünk)
                fs.WriteByte((byte)'B'); fs.WriteByte((byte)'M');
                int fileSize = 54 + (width * 3 + padding) * height;
                fs.Write(BitConverter.GetBytes(fileSize), 0, 4);
                fs.Write(BitConverter.GetBytes(0), 0, 4);
                fs.Write(BitConverter.GetBytes(54), 0, 4);
                fs.Write(BitConverter.GetBytes(40), 0, 4);
                fs.Write(BitConverter.GetBytes(width), 0, 4);
                fs.Write(BitConverter.GetBytes(height), 0, 4);
                fs.Write(BitConverter.GetBytes((short)1), 0, 2);
                fs.Write(BitConverter.GetBytes((short)24), 0, 2);
                fs.Write(BitConverter.GetBytes(0), 0, 4);
                fs.Write(BitConverter.GetBytes(0), 0, 4);
                fs.Write(BitConverter.GetBytes(0), 0, 4);
                fs.Write(BitConverter.GetBytes(0), 0, 4);
                fs.Write(BitConverter.GetBytes(0), 0, 4);
                fs.Write(BitConverter.GetBytes(0), 0, 4);

                // Színek (élénk, jól látható)
                byte[][] colors = new byte[][]
                {
                    new byte[] {200,  70,  70},  // mélyvörös
                    new byte[] { 70, 100, 160},  // szép kékesszürke
                    new byte[] {220, 180, 140},  // meleg bézs
                    new byte[] { 80, 140, 100},  // visszafogott zöld
                    new byte[] {150, 110, 150}   // halvány lila
                };

                // BMP-ben alulról felfelé írunk
                for (int y = 0; y <height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int c = pixCluster[y * width + x];
                        var col = colors[c % colors.Length];
                        fs.WriteByte(col[2]); // B
                        fs.WriteByte(col[1]); // G
                        fs.WriteByte(col[0]); // R
                    }
                    fs.Write(padBytes, 0, padding);
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
            ImageSegmentation problemMug = new ImageSegmentation();
            problemMug.LoadImageFromFile("mug.bmp");
            problemMug.KMeansClustering(2, 100);
            problemMug.SavePixClusterToFile("outputmug.bmp");

            Console.WriteLine("\n\n\n");

            ImageSegmentation problemCockatiel = new ImageSegmentation();
            problemCockatiel.LoadImageFromFile("cockatiel.bmp");
            problemCockatiel.KMeansClustering(3, 100);
            problemCockatiel.SavePixClusterToFile("outputcockatiel.bmp");
        }
    }
}