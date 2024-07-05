// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using Microsoft.Extensions.Azure;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedPaste.Plugins
{
    public class ClipboardService
    {
        private enum ClipboardFormat
        {
            Text,
            Html,
            Image,
            File,
        }

        private DataPackage _clipboardDataPackage = new DataPackage();

        public ClipboardService()
        {
        }

        public async Task<bool> ResetData(DataPackageView clipboardData)
        {
            DataPackageView clipboardContent = clipboardData;

            // Create a copy of clipboardData in _clipboardDataPackage
            _clipboardDataPackage = new DataPackage();

            if (clipboardContent == null)
            {
                return false;
            }

            if (clipboardContent.Contains(StandardDataFormats.Text))
            {
                string clipboardText = await ClipboardHelper.GetClipboardTextContent(clipboardContent);
                _clipboardDataPackage.SetText(clipboardText);
            }

            if (clipboardContent.Contains(StandardDataFormats.Html))
            {
                string clipboardHtml = await ClipboardHelper.GetClipboardHTMLContent(clipboardContent);
                _clipboardDataPackage.SetHtmlFormat(clipboardHtml);
            }

            if (clipboardContent.Contains(StandardDataFormats.Bitmap))
            {
                RandomAccessStreamReference clipboardImage = await ClipboardHelper.GetClipboardBitmapContent(clipboardContent);
                _clipboardDataPackage.SetBitmap(clipboardImage);
            }

            if (clipboardContent.Contains(StandardDataFormats.StorageItems))
            {
                // Get storage items and iterate through their file names to find endings
                // to enable audio and image to text
                try
                {
                    var storageItems = await clipboardContent.GetStorageItemsAsync();
                    _clipboardDataPackage.SetStorageItems(storageItems);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return true;
        }

        private async Task<Tuple<List<ClipboardFormat>, List<string>>> GetAvailableFormats(DataPackageView clipboardData)
        {
            List<ClipboardFormat> clipboardFormats = new List<ClipboardFormat>();
            List<string> clipboardFileExtensions = new List<string>();

            Tuple<List<ClipboardFormat>, List<string>> returnTuple = new Tuple<List<ClipboardFormat>, List<string>>(clipboardFormats, clipboardFileExtensions);

            if (clipboardData == null)
            {
                return returnTuple;
            }

            if (clipboardData.Contains(StandardDataFormats.Text))
            {
                clipboardFormats.Add(ClipboardFormat.Text);
            }

            if (clipboardData.Contains(StandardDataFormats.Html))
            {
                clipboardFormats.Add(ClipboardFormat.Html);
            }

            if (clipboardData.Contains(StandardDataFormats.Bitmap))
            {
                clipboardFormats.Add(ClipboardFormat.Image);
            }

            if (clipboardData.Contains(StandardDataFormats.StorageItems))
            {
                // Get storage items and iterate through their file names to find endings
                // to enable audio and image to text
                clipboardFormats.Add(ClipboardFormat.File);
                try
                {
                    var storageItems = await clipboardData.GetStorageItemsAsync();
                    _clipboardDataPackage.SetStorageItems(storageItems);
                    foreach (var storageItem in storageItems)
                    {
                        if (storageItem is Windows.Storage.StorageFile file)
                        {
                            string fileExtension = file.FileType.ToLowerInvariant();
                            clipboardFileExtensions.Add(fileExtension);
                        }
                        else if (storageItem is Windows.Storage.StorageFolder)
                        {
                            clipboardFileExtensions.Add("folder");
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return returnTuple;
        }

        public string GetClipboardText()
        {
            DataPackageView currentClipboardView = _clipboardDataPackage.GetView();
            string clipboardText = ClipboardHelper.GetClipboardTextContent(currentClipboardView).Result;
            return clipboardText;
        }

        public bool SetClipboardText(string inputText)
        {
            _clipboardDataPackage.SetText(inputText);
            return true;
        }

        public string GetClipboardHtml()
        {
            DataPackageView currentClipboardView = _clipboardDataPackage.GetView();
            string clipboardHtml = ClipboardHelper.GetClipboardHTMLContent(currentClipboardView).Result;
            return clipboardHtml;
        }

        public bool SetClipboardHtml(string htmlContent)
        {
            _clipboardDataPackage.SetHtmlFormat(HtmlFormatHelper.CreateHtmlFormat(htmlContent));
            return true;
        }

        public async Task<SoftwareBitmap> GetClipboardImage()
        {
            DataPackageView currentClipboardView = _clipboardDataPackage.GetView();
            SoftwareBitmap clipboardImage = await ClipboardHelper.GetClipboardImageContent(currentClipboardView);
            return clipboardImage;
        }

        public bool SetClipboardStorageItems(List<StorageFile> inputItems)
        {
            _clipboardDataPackage.SetStorageItems(inputItems);
            return true;
        }

        public DataPackageView GetDataPackageView()
        {
            return _clipboardDataPackage.GetView();
        }

        public bool PutDataPackageIntoClipboard()
        {
            Clipboard.SetContent(_clipboardDataPackage);
            return true;
        }

        public List<string> GetClipboardFormats()
        {
            DataPackageView currentClipboardView = _clipboardDataPackage.GetView();
            Tuple<List<ClipboardFormat>, List<string>> clipboardFormatData = GetAvailableFormats(currentClipboardView).Result;
            List<ClipboardFormat> clipboardFormats = clipboardFormatData.Item1;
            List<string> returnList = clipboardFormats.Select(x => x.ToString()).ToList();
            return returnList;
        }

        public List<string> GetClipboardFileExtension()
        {
            DataPackageView currentClipboardView = _clipboardDataPackage.GetView();
            Tuple<List<ClipboardFormat>, List<string>> clipboardFormatData = GetAvailableFormats(currentClipboardView).Result;
            List<string> clipboardExtensions = clipboardFormatData.Item2;
            return clipboardExtensions;
        }
    }
}
