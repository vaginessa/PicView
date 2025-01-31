﻿using ImageMagick;
using PicView.UILogic;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Rotation = PicView.UILogic.TransformImage.Rotation;

namespace PicView.ImageHandling
{
    internal static class ImageDecoder
    {
        /// <summary>
        /// Decodes image from file to BitMapSource
        /// </summary>
        /// <param name="fileInfo">Cannot be null</param>
        /// <returns></returns>
        internal static async Task<BitmapSource?> ReturnBitmapSourceAsync(FileInfo fileInfo)
        {
            if (fileInfo == null || fileInfo.Length <= 0) { return null; }

            var extension = fileInfo.Extension.ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                case ".jpe":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".jfif":
                case ".ico":
                case ".webp":
                case ".wbmp":
                    return await GetWriteableBitmapAsync(fileInfo).ConfigureAwait(false);

                case ".tga":
                    return await GetDefaultBitmapSourceAsync(fileInfo, true).ConfigureAwait(false);

                case ".svg":
                    return await GetTransparentBitmapSourceAsync(fileInfo, MagickFormat.Svg).ConfigureAwait(false);

                case ".b64":
                    return await Base64.Base64StringToBitmapAsync(fileInfo).ConfigureAwait(false);

                default:
                    return await GetDefaultBitmapSourceAsync(fileInfo).ConfigureAwait(false);
            }
        }

        #region Render Image From Source

        /// <summary>
        /// Returns the currently viewed bitmap image to MagickImage
        /// </summary>
        /// <returns></returns>
        internal static MagickImage? GetRenderedMagickImage()
        {
            try
            {
                var frame = BitmapFrame.Create(GetRenderedBitmapFrame());
                var encoder = new PngBitmapEncoder();

                encoder.Frames.Add(frame);

                var magickImage = new MagickImage();
                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    magickImage.Read(stream.ToArray());
                }

                magickImage.Quality = 100;

                // Apply rotation and flip transformations
                if (Rotation.Flipped)
                {
                    magickImage.Flop();
                }
                magickImage.Rotate(Rotation.RotationAngle);

                return magickImage;
            }
            catch (Exception e)
            {
#if DEBUG
                Trace.WriteLine($"{nameof(GetRenderedMagickImage)} exception, \n {e.Message}");
#endif
                return null;
            }
        }

        /// <summary>
        /// Returns Displayed image source to a BitmapFrame
        /// </summary>
        /// <returns></returns>
        internal static BitmapFrame? GetRenderedBitmapFrame()
        {
            try
            {
                var sourceBitmap = ConfigureWindows.GetMainWindow.MainImage.Source as BitmapSource;

                if (sourceBitmap == null)
                {
                    return null;
                }

                var effect = ConfigureWindows.GetMainWindow.MainImage.Effect;

                var rectangle = new Rectangle
                {
                    Fill = new ImageBrush(sourceBitmap),
                    Effect = effect
                };

                var sourceSize = new Size(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight);
                rectangle.Measure(sourceSize);
                rectangle.Arrange(new Rect(sourceSize));

                var renderedBitmap = new RenderTargetBitmap(sourceBitmap.PixelWidth, sourceBitmap.PixelHeight, sourceBitmap.DpiX, sourceBitmap.DpiY, PixelFormats.Default);
                renderedBitmap.Render(rectangle);

                BitmapFrame bitmapFrame = BitmapFrame.Create(renderedBitmap);
                bitmapFrame.Freeze();

                return bitmapFrame;
            }
            catch (Exception exception)
            {
#if DEBUG
                Trace.WriteLine($"{nameof(GetRenderedBitmapFrame)} exception, \n {exception.Message}");
#endif
                return null;
            }
        }

        #endregion Render Image From Source

        #region Private functions

        /// <summary>
        /// Create MagickImage and make sure its transparent, return it as BitmapSource
        /// </summary>
        /// <param name="fileInfo"></param>
        /// <param name="magickFormat"></param>
        /// <returns></returns>
        private static async Task<BitmapSource?> GetTransparentBitmapSourceAsync(FileInfo fileInfo, MagickFormat magickFormat)
        {
            using FileStream filestream = new(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
            var data = new byte[filestream.Length];
            await filestream.ReadAsync(data.AsMemory(0, (int)filestream.Length)).ConfigureAwait(false);

            using var magickImage = new MagickImage(data)
            {
                Quality = 100,
                ColorSpace = ColorSpace.Transparent,
                BackgroundColor = MagickColors.Transparent,
                Format = magickFormat,
                Settings =
                {
                    Format = magickFormat,
                    BackgroundColor = MagickColors.Transparent,
                    FillColor = MagickColors.Transparent,
                },
            };

            var bitmap = magickImage.ToBitmapSource();
            bitmap.Freeze();
            return bitmap;
        }

        private static async Task<WriteableBitmap?> GetWriteableBitmapAsync(FileInfo fileInfo)
        {
            try
            {
                using var stream = File.OpenRead(fileInfo.FullName);
                var data = new byte[stream.Length];
                await stream.ReadAsync(data.AsMemory(0, (int)stream.Length)).ConfigureAwait(false);

                var sKBitmap = SKBitmap.Decode(data);
                if (sKBitmap is null) { return null; }

                var skPic = sKBitmap.ToWriteableBitmap();
                sKBitmap.Dispose();

                skPic.Freeze();
                return skPic;
            }
            catch (Exception e)
            {
#if DEBUG
                Trace.WriteLine($"{nameof(GetWriteableBitmapAsync)} {fileInfo.Name} exception: \n {e.Message}");
#endif
                return null;
            }
        }

        private static async Task<BitmapSource?> GetDefaultBitmapSourceAsync(FileInfo fileInfo, bool autoOrient = false)
        {
            try
            {
                using var magick = new MagickImage()
                {
                    Quality = 100
                };

                await magick.ReadAsync(fileInfo).ConfigureAwait(false);
                if (autoOrient)
                {
                    magick.AutoOrient();
                }

                var pic = magick.ToBitmapSource();
                pic.Freeze();

                return pic;
            }
            catch (Exception e)
            {
#if DEBUG
                Trace.WriteLine($"{nameof(GetDefaultBitmapSourceAsync)} {fileInfo.Name} exception, \n {e.Message}");
#endif
                Tooltip.ShowTooltipMessage(e);
                return null;
            }
        }

        #endregion Private functions
    }
}