// Copyright © 2020 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using Xunit;
using CefSharp.Extensions.ModelBinding;

namespace CefSharp.Extensions.Test.ModelBinding
{
    public class ModelBindingExtensionsFacts
    {
        [Theory]
        [InlineData("A", "A")]
        [InlineData("AB", "AB")]
        [InlineData("AString", "aString")]
        [InlineData("aString", "aString")]
        public void ConvertStringToCamelCaseTheory(string input, string expected)
        {
            var actual = input.ConvertNameToCamelCase();

            Assert.Equal(expected, actual);
        }
    }
}
