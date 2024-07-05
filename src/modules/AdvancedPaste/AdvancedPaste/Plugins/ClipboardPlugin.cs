// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Enumeration;
using System.Threading.Tasks;
using AdvancedPaste.Helpers;
using Microsoft.SemanticKernel;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AdvancedPaste.Plugins
{
    public class ClipboardPlugin
    {
        private readonly ClipboardService _clipboardService;
        private readonly AICompletionsHelper _aiCompletionsHelper;

        public ClipboardPlugin(ClipboardService clipboardService, AICompletionsHelper aiCompletionsHelper)
        {
            _clipboardService = clipboardService;
            _aiCompletionsHelper = aiCompletionsHelper;
        }

        [KernelFunction("get_clipboard_formats")]
        [Description("Get what formats are currently on the clipboard.")]
        [return: Description("Array of available formats")]
        public List<string> GetFormats()
        {
            return _clipboardService.GetClipboardFormats();
        }

        [KernelFunction("transform_to_json")]
        [Description("Takes clipboard text and transforms it to JSON. Clipboard text needs to be XML for this to work.")]
        [return: Description("Array of available clipboard formats after operation")]
        public List<string> TransformToJSON()
        {
            string clipboardText = _clipboardService.GetClipboardText();
            string jsonText = JsonHelper.ToJsonFromXmlOrCsv(_clipboardService.GetDataPackageView());
            _clipboardService.SetClipboardText(jsonText);
            return _clipboardService.GetClipboardFormats();
        }

        [KernelFunction("transform_text_with_custom_instructions")]
        [Description("Takes an input instruction and formats any text on the clipboard with that custom input instruction. This uses AI to accomplish the task. All requests must be phrased as: 'Paste as...', like 'Paste as markdown table' or 'Paste as bulleted list' and should be as descriptive as is reasonable.")]
        [return: Description("Array of available clipboard formats after operation")]
        public List<string> TransformTextFormattedWithCustomInstructions(string inputInstructions)
        {
            string clipboardText = _clipboardService.GetClipboardText();
            AICompletionsHelper.AICompletionsResponse formattedText = _aiCompletionsHelper.AIFormatString(inputInstructions, clipboardText);
            _clipboardService.SetClipboardText(formattedText.Response);
            return _clipboardService.GetClipboardFormats();
        }

        [KernelFunction("transform_to_file")]
        [Description("If the clipboard has text, HTML, or image data on it, this function will transform it into file data instead. Allows user to paste things as a file.")]
        [return: Description("Array of available clipboard formats after operation")]
        public async Task<List<string>> TransformToFile()
        {
            List<string> availableClipboardFormats = _clipboardService.GetClipboardFormats();

            string fileName = null;

            if (availableClipboardFormats.Contains("Text"))
            {
                string clipboardText = _clipboardService.GetClipboardText();
                fileName = await ToFileFunction(clipboardText);
            }
            else if (availableClipboardFormats.Contains("Image"))
            {
                SoftwareBitmap softwareBitmap = await _clipboardService.GetClipboardImage();
                fileName = await ToFileFunction(softwareBitmap);
            }

            if (fileName != null)
            {
                StorageFile storageFile = await StorageFile.GetFileFromPathAsync(fileName).AsTask();

                List<StorageFile> storageList = new List<StorageFile>
                {
                    storageFile,
                };

                _clipboardService.SetClipboardStorageItems(storageList);
            }

            return _clipboardService.GetClipboardFormats();
        }

        internal async Task<string> ToFileFunction(string inputContent)
        {
            // Create a local file in the temp directory
            string tempFileName = Path.Combine(Path.GetTempPath(), "clipboard.txt");

            // Write the content to the file
            await File.WriteAllTextAsync(tempFileName, inputContent);

            return tempFileName;
        }

        internal async Task<string> ToFileFunction(SoftwareBitmap softwareBitmap)
        {
            // Create a local file in the temp directory
            string tempFileName = Path.Combine(Path.GetTempPath(), "clipboard.png");

            using (var stream = new InMemoryRandomAccessStream())
            {
                // Encode the SoftwareBitmap to the stream
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetSoftwareBitmap(softwareBitmap);
                await encoder.FlushAsync();

                // Set the stream position to the beginning
                stream.Seek(0);

                // Create a new file in the temporary directory with a .png extension
                using (var fileStream = File.Create(tempFileName))
                {
                    await stream.AsStream().CopyToAsync(fileStream);
                }
            }

            return tempFileName;
        }
    }
}
