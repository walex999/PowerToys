// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Helpers
{
    public class SLMChatCompletionService : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object> Attributes => throw new NotImplementedException();

        public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings executionSettings = null, Kernel kernel = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.Delay(1000, cancellationToken);
            return chatHistory;
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(ChatHistory chatHistory, PromptExecutionSettings executionSettings = null, Kernel kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Example StreamingChatMessageContent object to demonstrate appending 's' to its Content
            var messageContent = new StreamingChatMessageContent(AuthorRole.Assistant, string.Empty);

            for (int i = 0; i < 10; i++)
            {
                // Append 's' to the Content property
                messageContent.Content += "s";

                // Yield the updated message content back to the caller
                yield return messageContent;

                // Wait for 1 second before continuing the loop
                // Also, observe the cancellationToken to stop if cancellation is requested
                await Task.Delay(1000, cancellationToken);
            }
        }
    }
}
