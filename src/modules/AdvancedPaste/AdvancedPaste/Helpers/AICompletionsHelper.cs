// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.Common;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using AdvancedPaste.Plugins;
using Azure;
using Azure.AI.OpenAI;
using Helpers;
using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Telemetry;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TextGeneration;
using Windows.Security.Credentials;

namespace AdvancedPaste.Helpers
{
    public class AICompletionsHelper : IDisposable
    {
        // Return Response and Status code from the request.
        public struct AICompletionsResponse
        {
            public AICompletionsResponse(string response, int apiRequestStatus)
            {
                Response = response;
                ApiRequestStatus = apiRequestStatus;
            }

            public string Response { get; }

            public int ApiRequestStatus { get; }
        }

        public delegate void CompletionUpdatedEventHandler(string newCompletion);

        public event CompletionUpdatedEventHandler CompletionUpdated;

        private string _openAIKey;

        private string _modelName = "gpt-3.5-turbo-0125";

        public bool IsAIEnabled => !string.IsNullOrEmpty(this._openAIKey);

        private SLMRunner _slmRunner;

        private CancellationTokenSource cts;

        private IKernelBuilder _kernelBuilder;

        private Kernel _kernel;

        private IChatCompletionService _chatCompletionService;

        private OpenAIPromptExecutionSettings _openAIPromptExecutionSettings;

        private ChatHistory _pasteAgentChatHistory;

        private ClipboardService _clipboardService;

        private SLMChatCompletionService _slmChatCompletionService;

        public AICompletionsHelper(ChatHistory inChatHistory, ClipboardService clipboardService)
        {
            _pasteAgentChatHistory = inChatHistory;
            _slmRunner = new SLMRunner();
            _slmChatCompletionService = new SLMChatCompletionService();

            _clipboardService = clipboardService;

            _kernelBuilder = Kernel.CreateBuilder();

            _kernelBuilder.Services.AddKeyedSingleton<IChatCompletionService>("SLMChat", _slmChatCompletionService);

            _kernelBuilder.Services.AddSingleton(_clipboardService);
            _kernelBuilder.Services.AddSingleton(this);

            _kernelBuilder.Plugins.AddFromType<ClipboardPlugin>();

            // Build the kernel
            _kernel = _kernelBuilder.Build();
            _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

            _openAIPromptExecutionSettings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            };
        }

        public async Task<bool> StartLoadingModel()
        {
            await _slmRunner.InitializeAsync();
            return true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_slmRunner != null)
                {
                    _slmRunner.Dispose();
                    _slmRunner = null;
                }
            }
        }

        public void SetOpenAIKey(string openAIKey)
        {
            this._openAIKey = openAIKey;
        }

        public string GetKey()
        {
            return _openAIKey;
        }

        public static string LoadOpenAIKey()
        {
            PasswordVault vault = new PasswordVault();

            try
            {
                PasswordCredential cred = vault.Retrieve("https://platform.openai.com/api-keys", "PowerToys_AdvancedPaste_OpenAIKey");
                if (cred is not null)
                {
                    return cred.Password.ToString();
                }
            }
            catch (Exception)
            {
            }

            return string.Empty;
        }

        protected virtual void OnCompletionUpdated(string newCompletion)
        {
            CompletionUpdated?.Invoke(newCompletion);
        }

        public async Task<string> GetLocalAICompletion(string inputInstructions, string inputString)
        {
            string systemInstructions = $@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.

Do not output anything else besides the reformatted clipboard content, so no chatter, or explanations.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}
";

            if (cts != null)
            {
                await cts.CancelAsync();
                cts = null;
            }

            cts = new CancellationTokenSource();

            string prompt = $@"<|system>|
{systemInstructions}
<|end|>
<|user|>
{userMessage}
<|end|>
<|assistant|>
Output: 
";

            var fullResult = string.Empty;

            await foreach (var partialResult in _slmRunner.InferStreamingAsync(prompt).WithCancellation(cts.Token))
            {
                OnCompletionUpdated(fullResult);
                fullResult += partialResult;

                // TODO: Remove this. Total hack to make the UI thread not pause.
                await Task.Delay(1);
            }

            return fullResult;
        }

        public async Task<string> GetAIAgentCompletion(string inputInstructions)
        {
            _pasteAgentChatHistory.Clear();

            _pasteAgentChatHistory.AddSystemMessage("""
                You are an agent who is tasked with helping users paste their clipboard data. You have functions available to help you with this task.
                The user will put in a request to format their clipboard data and you will fulfill it. Start always by checking the available clipboard formats.
                """);

            _pasteAgentChatHistory.AddUserMessage(inputInstructions);

            try
            {
                var result = await _chatCompletionService.GetChatMessageContentAsync(_pasteAgentChatHistory, executionSettings: _openAIPromptExecutionSettings, kernel: _kernel);
                _pasteAgentChatHistory.AddAssistantMessage(result.Content);
                return result.Content;
            }
            catch (Exception e)
            {
                return e.Message;
            }
        }

        private Response<Completions> GetAICompletion(string systemInstructions, string userMessage)
        {
            OpenAIClient azureAIClient = new OpenAIClient(_openAIKey);

            var response = azureAIClient.GetCompletions(
                new CompletionsOptions()
                {
                    DeploymentName = "gpt-3.5-turbo-instruct",
                    Prompts =
                    {
                        systemInstructions + "\n\n" + userMessage,
                    },
                    Temperature = 0.01F,
                    MaxTokens = 2000,
                });

            if (response.Value.Choices[0].FinishReason == "length")
            {
                Console.WriteLine("Cut off due to length constraints");
            }

            return response;
        }

        public AICompletionsResponse AIFormatString(string inputInstructions, string inputString)
        {
            string systemInstructions = $@"You are tasked with reformatting user's clipboard data. Use the user's instructions, and the content of their clipboard below to edit their clipboard content as they have requested it.

Do not output anything else besides the reformatted clipboard content.";

            string userMessage = $@"User instructions:
{inputInstructions}

Clipboard Content:
{inputString}

Output:
";

            string aiResponse = null;
            Response<Completions> rawAIResponse = null;
            int apiRequestStatus = (int)HttpStatusCode.OK;
            try
            {
                rawAIResponse = this.GetAICompletion(systemInstructions, userMessage);
                aiResponse = rawAIResponse.Value.Choices[0].Text;

                int promptTokens = rawAIResponse.Value.Usage.PromptTokens;
                int completionTokens = rawAIResponse.Value.Usage.CompletionTokens;
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomFormatEvent(promptTokens, completionTokens, _modelName));
            }
            catch (Azure.RequestFailedException error)
            {
                Logger.LogError("GetAICompletion failed", error);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                apiRequestStatus = error.Status;
            }
            catch (Exception error)
            {
                Logger.LogError("GetAICompletion failed", error);
                PowerToysTelemetry.Log.WriteEvent(new Telemetry.AdvancedPasteGenerateCustomErrorEvent(error.Message));
                apiRequestStatus = -1;
            }

            return new AICompletionsResponse(aiResponse, apiRequestStatus);
        }
    }
}
