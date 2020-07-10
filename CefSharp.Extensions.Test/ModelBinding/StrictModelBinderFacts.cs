// Copyright © 2020 The CefSharp Authors. All rights reserved.
//
// Use of this source code is governed by a BSD-style license that can be found in the LICENSE file.

using System;
using System.Collections.Generic;
using CefSharp.Extensions.ModelBinding;
using CefSharp.ModelBinding;
using Xunit;

namespace CefSharp.Extensions.Test.ModelBinding
{
    /// <summary>
    /// StrictModelBinderFacts - Test Cases for the <see cref="StrictModelBinder"/>
    /// </summary>
    public class StrictModelBinderFacts
    {
        private enum TestEnum
        {
            A,
            B,
            C
        }

        private class TestObject
        {
            public string AString { get; set; }
            public bool ABool { get; set; }
            public int AnInteger { get; set; }
            public double ADouble { get; set; }
            public TestEnum AnEnum { get; set; }
        }

        [Fact]
        public void BindsComplexObjects()
        {
            IBinder binder = new StrictModelBinder();
            var obj = new Dictionary<string, object>
            {
                { "anEnum", 2 },
                { "aString", "SomeValue" },
                { "aBool", true },
                { "anInteger", 2.4 },
                { "aDouble", 2.6 }
            };

            var result = (TestObject)binder.Bind(obj, typeof(TestObject));

            Assert.Equal(TestEnum.C, result.AnEnum);
            Assert.Equal(obj["AString"], result.AString);
            Assert.Equal(obj["ABool"], result.ABool);
            Assert.Equal(2, result.AnInteger);
            Assert.Equal(obj["ADouble"], result.ADouble);
        }

        [Fact]
        public void BindsEnums()
        {
            IBinder binder = new StrictModelBinder();
            var result = binder.Bind(2, typeof(TestEnum));

            Assert.Equal(TestEnum.C, result);
        }

        [Fact]
        public void BindsIntegersWithPrecisionLoss()
        {
            IBinder binder = new StrictModelBinder();
            var result = binder.Bind(2.5678, typeof(int));

            Assert.Equal(3, result);

            result = binder.Bind(2.123, typeof(int));

            Assert.Equal(2, result);
        }

        [Fact]
        public void BindsDoublesWithoutPrecisionLoss()
        {
            const double Expected = 2.5678;
            IBinder binder = new StrictModelBinder();
            var result = binder.Bind(Expected, typeof(double));

            Assert.Equal(Expected, result);

            result = binder.Bind(2, typeof(double));

            Assert.Equal(2.0, result);
        }

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(double))]
        [InlineData(typeof(bool))]
        public void NullToValueTypeTheory(Type conversionType)
        {
            IBinder binder = new StrictModelBinder();

            //Throw Exception if we try and conver null to a ValueType
            Assert.Throws<ModelBindingException>(() => binder.Bind(null, conversionType));
        }

        [Fact]
        public void BindArrayWithNullElementToIntArray()
        {
            var arrayType = typeof(int[]);

            IBinder binder = new StrictModelBinder();
            var obj = new List<object> { 10, 20, null, 30 };
            var result = binder.Bind(obj, arrayType);

            Assert.NotNull(result);
            Assert.Equal(arrayType, result.GetType());

            var arr = (int[])result;
            Assert.Equal(obj.Count, arr.Length);

            for (int i = 0; i < obj.Count; i++)
            {
                var expected = obj[i] ?? 0;
                var actual = arr[i];
                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void BindListOfNumbersToDoubleArray()
        {
            var doubleArrayType = typeof(double[]);

            IBinder binder = new StrictModelBinder();
            var obj = new List<object> { 10, 20, 1.23 };
            var result = binder.Bind(obj, doubleArrayType);

            Assert.NotNull(result);
            Assert.Equal(doubleArrayType, result.GetType());

            var arr = (double[])result;
            Assert.Equal(obj.Count, arr.Length);

            for (int i = 0; i < obj.Count; i++)
            {
                var expected = Convert.ToDouble(obj[i]);
                var actual = arr[i];
                Assert.Equal(expected, actual);
            }
        }
    }
}
