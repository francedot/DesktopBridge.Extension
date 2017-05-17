using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Storage.Search;
using DesktopBridge.Extension.SampleApp.Models;

namespace DesktopBridge.Extension.SampleApp.Services
{
    public class CodeSnippetProvider
    {
        public async Task<IEnumerable<CodeSnippet>> GetCodeSnippetsAsync()
        {
            var result = new List<CodeSnippet>();

            var snippetFolder = await Package.Current.InstalledLocation.GetFolderAsync("Snippets");
            var snippetFiles = await snippetFolder.GetFilesAsync(CommonFileQuery.OrderByName);
            foreach (var snippetFile in snippetFiles)
            {
                result.Add(new CodeSnippet
                {
                    Title = snippetFile.Name,
                    Content = await Windows.Storage.FileIO.ReadTextAsync(snippetFile)
                });
            }

            return result;
        }
    }
}