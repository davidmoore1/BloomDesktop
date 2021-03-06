﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bloom.ImageProcessing;
using NUnit.Framework;
using Palaso.TestUtilities;
using Palaso.UI.WindowsForms.ImageToolbox;

namespace BloomTests.ImageProcessing
{
	[TestFixture]
	public class ImageUtilsTests
	{
		private const string _pathToTestImages = "src/BloomTests/ImageProcessing/images";

		[Test]
		public void ShouldChangeFormatToJpeg_Photo_True()
		{
			var path = Palaso.IO.FileLocator.GetFileDistributedWithApplication(_pathToTestImages, "man.jpg");
			Assert.IsTrue(ImageUtils.ShouldChangeFormatToJpeg(Image.FromFile(path)));
		}

		[Test]
		public void ShouldChangeFormatToJpeg_OneColor_False()
		{
			var path = Palaso.IO.FileLocator.GetFileDistributedWithApplication(_pathToTestImages, "bird.png");
			Assert.IsFalse(ImageUtils.ShouldChangeFormatToJpeg(Image.FromFile(path)));
		}

		[Test]
		public void ProcessAndSaveImageIntoFolder_PhotoButPNGFile_SavesAsJpeg()
		{
			ProcessAndSaveImageIntoFolder_AndTestResults("man.png", ImageFormat.Jpeg);
		}

		[Test]
		public void ProcessAndSaveImageIntoFolder_Photo_KeepsJpeg()
		{
			ProcessAndSaveImageIntoFolder_AndTestResults("man.jpg", ImageFormat.Jpeg);
		}
		
		[Test]
		public void ProcessAndSaveImageIntoFolder_OneColor_SavesAsPng()
		{
			ProcessAndSaveImageIntoFolder_AndTestResults("bird.png", ImageFormat.Png);
		}

		private static void ProcessAndSaveImageIntoFolder_AndTestResults(string testImageName, ImageFormat expectedOutputFormat)
		{
			var inputPath = Palaso.IO.FileLocator.GetFileDistributedWithApplication(_pathToTestImages, testImageName);
			var image = PalasoImage.FromFile(inputPath);
			using(var folder = new TemporaryFolder())
			{
				var fileName = ImageUtils.ProcessAndSaveImageIntoFolder(image, folder.Path);
				Assert.AreEqual(expectedOutputFormat == ImageFormat.Jpeg ? ".jpg" : ".png", Path.GetExtension(fileName));
				var outputPath = folder.Combine(fileName);
				using(var img = Image.FromFile(outputPath))
				{
					Assert.AreEqual(expectedOutputFormat, img.RawFormat);
				}
				var alternativeThatShouldNotBeThere = Path.Combine(Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath) + (expectedOutputFormat.Equals(ImageFormat.Jpeg) ? ".png" : ".jpg"));
				Assert.IsFalse(File.Exists(alternativeThatShouldNotBeThere),"Did not expect to have the file "+alternativeThatShouldNotBeThere);
			}
		}
	}
}
