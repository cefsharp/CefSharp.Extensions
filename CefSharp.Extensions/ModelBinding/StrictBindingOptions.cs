// Copyright © 2020 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

namespace CefSharp.Extensions.ModelBinding
{
    /// <summary>
    /// A 
    /// </summary>
    public class StrictBindingOptions : BindingOptions
    {
        public StrictBindingOptions()
        {
            Binder = new StrictModelBinder();
            MethodInterceptor = new StrictMethodInterceptor();
        }
    }
}
