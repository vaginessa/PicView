﻿using PicView.ChangeImage;
using PicView.PicGallery;
using PicView.Properties;
using PicView.SystemIntegration;
using System;
using System.Diagnostics;
using System.IO;
using static PicView.ChangeImage.Navigation;
using static PicView.FileHandling.FileLists;

namespace PicView.FileHandling
{
    internal static class ArchiveExtraction
    {
        // TODO needs improvement to be dynamic
        private const string SupportedFilesFilter =
            " *.jpg *.jpeg *.jpe *.png *.bmp *.tif *.tiff *.gif *.ico *.jfif *.webp *.wbmp "
            + "*.psd *.psb "
            + "*.tga *.dds "
            + "*.svg "
            + "*.3fr *.arw *.cr2 *.crw *.dcr *.dng *.erf *.kdc *.mdc *.mef *.mos *.mrw *.nef *.nrw *.orf "
            + "*.pef *.raf *.raw *.rw2 *.srf *.x3f "
            + "*.pgm *.hdr *.cut *.exr *.dib *.heic *.emf *.wmf *.wpg *.pcx *.xbm *.xpm";

        /// <summary>
        /// File path for the extracted folder
        /// </summary>
        internal static string? TempFilePath { get; set; }

        /// <summary>
        /// File path for the extracted zip file
        /// </summary>
        internal static string? TempZipFile { get; set; }

        /// <summary>
        /// Attemps to extract folder
        /// </summary>
        /// <param name="path">The path to the archived file</param>
        /// <returns></returns>
        internal static bool Extract(string path)
        {
            const string winRar = "WinRAR.exe";
            const string winRarPath = "\\WinRAR\\WinRAR.exe";

            const string sevenzip = "7z.exe";
            const string sevenzipPath = "\\7-Zip\\7z.exe";

            var appNames = new[] { winRar, sevenzip };
            var appPathNames = new[] { winRarPath, sevenzipPath };

            string? getextractPath = GetExtractApp(appPathNames, appNames);

            if (getextractPath == null) { return false; }

            return Extract(path, getextractPath, getextractPath.Contains("WinRAR", StringComparison.OrdinalIgnoreCase));
        }

        internal static string? GetExtractApp(string[] commonPath, string[] appName)
        {
            if (appName == null || commonPath == null) { return null; }

            for (int i = 0; i < commonPath.Length; i++)
            {
                string x86path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) + commonPath[i];
                if (File.Exists(x86path))
                {
                    return x86path;
                }
                string x64path = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + commonPath[i];
                if (File.Exists(x64path))
                {
                    return x64path;
                }
            }

            for (int i = 0; i < appName.Length; i++)
            {
                string? registryPath = NativeMethods.GetPathForExe(appName[i]);
                if (registryPath == null) { return null; }
                if (File.Exists(registryPath))
                {
                    return registryPath;
                }
            }
            return null;
        }

        /// <summary>
        /// Attemps to extract folder
        /// </summary>
        /// <param name="path">The path to the archived file</param>
        /// <param name="exe">Full path of the executeable</param>
        /// <param name="winrar">If WinRar or 7-Zip</param>
        private static bool Extract(string path, string exe, bool winrar)
        {
            if (CreateTempDirectory(path))
            {
#if DEBUG
                Trace.WriteLine("Created temp dir: " + TempFilePath);
#endif
            }
            else { return false; }

            // Create backup
            if (ErrorHandling.CheckOutOfRange() == false)
            {
                BackupPath = Pics[FolderIndex];
            }

            var arguments = winrar ?
                // Add WinRAR specifics
                "x -o- \"" + path + "\" "
                :
                // Add 7-Zip specifics
                "x \"" + path + "\" -o";

            arguments += TempFilePath + SupportedFilesFilter + " -r -aou";

            var x = Process.Start(new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                RedirectStandardOutput = true,

#if DEBUG
                CreateNoWindow = false
#else
                CreateNoWindow = true
#endif
            });

            if (x == null) { return false; }

            x.EnableRaisingEvents = true;
            x.BeginOutputReadLine();
            x.OutputDataReceived += async delegate
            {
                while (Pics.Count < 1 && x.HasExited == false)
                {
                    SetDirectory();
                }
                if (Pics.Count >= 1 && !x.HasExited)
                {
                    await LoadPic.LoadPicAtIndexAsync(0).ConfigureAwait(false);
                }
            };
            x.Exited += async delegate
            {
                if (SetDirectory())
                {
                    if (FolderIndex > 0)
                    {
                        await LoadPic.LoadPiFromFileAsync(Pics[0]).ConfigureAwait(false);
                    }

                    // Add zipped files as recent file
                    GetFileHistory.Add(TempZipFile);

                    if (Settings.Default.FullscreenGalleryHorizontal)
                    {
                        await GalleryLoad.Load().ConfigureAwait(false);
                    }
                }
                else
                {
                    await ErrorHandling.ReloadAsync(true).ConfigureAwait(false);
                }
            };

            return true;
        }

        internal static bool CreateTempDirectory(string path)
        {
            TempZipFile = path;
            TempFilePath = Path.GetTempPath() + Path.GetRandomFileName();
            Directory.CreateDirectory(TempFilePath);

            return Directory.Exists(TempFilePath);
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns></returns>
        private static bool SetDirectory()
        {
            if (string.IsNullOrEmpty(TempFilePath))
            {
#if DEBUG
                Trace.WriteLine("SetDirectory empty zip path");
#endif
                return false;
            }

            // Set extracted files to Pics
            if (!Directory.Exists(TempFilePath)) { return false; }

            var directory = Directory.GetDirectories(TempFilePath);
            if (directory.Length > 0)
            {
                TempFilePath = directory[0];
            }

            var extractedFiles = FileList(new FileInfo(TempFilePath));
            if (extractedFiles.Count > 0)
            {
                Pics = extractedFiles;
            }
            else
            {
                return false;
            }

            return true;
        }
    }
}