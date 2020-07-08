// Copyright © 2020 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.Threading.Tasks;

namespace CefSharp.Extensions
{
    public static class WebBrowserExtensions
    {
        /// <summary>
        /// An extension method that can be used to await the Loading of a web page
        /// Uses the <see cref="IWebBrowser.LoadingStateChanged"/> event to determine
        /// when the page has loaded.
        /// </summary>
        /// <param name="webBrowser">ChromiumWebBrowser instance</param>
        /// <param name="address">optional address</param>
        /// <returns>A task that represents the asynchronous loading of a web page and returns
        /// the result as a <see cref="CefErrorCode"/>. <see cref="CefErrorCode.None"/> if the load
        /// was successful.</returns>
        public static Task<CefErrorCode> LoadPageAsync(this IWebBrowser webBrowser, string address = null)
        {
            if(webBrowser.IsDisposed)
            {
                throw new ObjectDisposedException("webBrowser");
            }

            if (string.IsNullOrEmpty(address) && webBrowser.IsBrowserInitialized)
            {
                var browser = webBrowser.GetBrowser();
                if (browser.HasDocument && browser.IsLoading == false)
                {
                    //Address is null/empty and browser isn't loading
                    //so we'll return as browser is already loaded.
                    return Task.FromResult(CefErrorCode.None);
                }
            }

            var tcs = new TaskCompletionSource<CefErrorCode>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<LoadingStateChangedEventArgs> handler = null;
            //EventHandler<LoadErrorEventArgs> errorHandler = null;

            handler = (sender, args) =>
            {
                //Wait for while page to finish loading
                if (!args.IsLoading)
                {
                    webBrowser.LoadingStateChanged -= handler;
                    //webBrowser.LoadError -= errorHandler;

                    tcs.TrySetResult(CefErrorCode.None);
                }
            };

            //TODO: OnLoadingStateChange is being called before LoadError
            //which is incorrect according to https://magpcss.org/ceforum/apidocs3/projects/(default)/CefLoadHandler.html#OnLoadingStateChange(CefRefPtr%3CCefBrowser%3E,bool,bool,bool)
            //errorHandler = (sender, args) =>
            //{
            //    //We only care about main Frame errors
            //    if (args.Frame.IsMain)
            //    {
            //        webBrowser.LoadingStateChanged -= handler;
            //        webBrowser.LoadError -= errorHandler;

            //        ExecuteOnSyncContext(syncContext, () =>
            //        {
            //            tcs.TrySetResult(args.ErrorCode);
            //        });
            //    }
            //};

            //webBrowser.LoadError += errorHandler;
            webBrowser.LoadingStateChanged += handler;

            if (!string.IsNullOrEmpty(address))
            {
                webBrowser.Load(address);
            }

            return tcs.Task;
        }
    }
}
