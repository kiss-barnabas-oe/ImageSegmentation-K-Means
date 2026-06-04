# Image Segmentation using K-Means Clustering

A C# implementation of image segmentation based on the K-Means clustering algorithm.

The application loads a BMP image, groups pixels into clusters according to their RGB color values, and generates a segmented output image where each cluster is represented by a distinct color.

## Features

* BMP image loading and processing
* RGB color space clustering
* K-Means clustering algorithm
* Pixel classification into color-based segments
* Segmented image export
* Histogram generation for grayscale images

## Algorithm Overview

1. Load a BMP image and extract pixel data.
2. Initialize cluster centroids with random RGB values.
3. Assign each pixel to the nearest centroid using color distance.
4. Recalculate centroid positions based on assigned pixels.
5. Repeat until convergence or the maximum number of iterations is reached.
6. Generate a segmented output image.

## Technologies

* C#
* .NET
* Image Processing
* K-Means Clustering
* File I/O
* Object-Oriented Programming

## Learning Outcomes

This project demonstrates:

* Implementation of an unsupervised machine learning algorithm
* Digital image processing fundamentals
* Pixel-level image manipulation
* Distance-based clustering techniques
* Working with binary file formats (BMP)
* Data analysis and classification
