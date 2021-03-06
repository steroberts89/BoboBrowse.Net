﻿//* Bobo Browse Engine - High performance faceted/parametric search implementation 
//* that handles various types of semi-structured data.  Originally written in Java.
//*
//* Ported and adapted for C# by Shad Storhaug.
//*
//* Copyright (C) 2005-2015  John Wang
//*
//* Licensed under the Apache License, Version 2.0 (the "License");
//* you may not use this file except in compliance with the License.
//* You may obtain a copy of the License at
//*
//*   http://www.apache.org/licenses/LICENSE-2.0
//*
//* Unless required by applicable law or agreed to in writing, software
//* distributed under the License is distributed on an "AS IS" BASIS,
//* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//* See the License for the specific language governing permissions and
//* limitations under the License.

// Version compatibility level: 4.0.2
namespace BoboBrowse.Tests
{
    using BoboBrowse.Net;
    using BoboBrowse.Net.Facets;
    using BoboBrowse.Net.Facets.Impl;
    using BoboBrowse.Net.Index;
    using BoboBrowse.Net.Index.Digest;
    using BoboBrowse.Net.Support.Logging;
    using Lucene.Net.Documents;
    using Lucene.Net.Index;
    using Lucene.Net.Search;
    using Lucene.Net.Store;
    using NUnit.Framework;
    using System;
    using System.Collections.Generic;
    using System.Text;

    [TestFixture]
    public class FacetNotValuesTest
    {
        private static readonly ILog log = LogProvider.For<FacetNotValuesTest>();
        private List<IFacetHandler> _facetHandlers;
        private int _documentSize;
        private static string[] _idRanges = new string[] { "[10 TO 10]" };

        private class TestDataDigester : DataDigester
        {
            private Document[] _data;

            public TestDataDigester(List<IFacetHandler> facetHandlers, Document[] data)
            {
                _data = data;
            }

            public override void Digest(DataDigester.IDataHandler handler)
            {
                for (int i = 0; i < _data.Length; ++i)
                {
                    handler.HandleDocument(_data[i]);
                }
            }
        }

        [SetUp]
        public void Init()
        {
            _facetHandlers = CreateFacetHandlers();

            _documentSize = 2;
            //string confdir = System.getProperty("conf.dir");
            //if (confdir == null) confdir = "./resource";
            //System.setProperty("log.home", ".");
            //org.apache.log4j.PropertyConfigurator.configure(confdir + "/log4j.properties");
        }

        [TearDown]
        public void Dispose()
        {
            _facetHandlers = null;
            _documentSize = 0;
        }

        public Document[] CreateDataTwo()
        {
            List<Document> dataList = new List<Document>();
            string color = "red";
            string ID = "10";
            Document d = new Document();
            d.Add(new StringField("id", ID, Field.Store.YES));
            d.Add(new StringField("color", color, Field.Store.YES));
            d.Add(new Int32Field("NUM", 10, Field.Store.YES));
            dataList.Add(d);

            color = "green";
            ID = "11";
            d = new Document();
            d.Add(new StringField("id", ID, Field.Store.YES));
            d.Add(new StringField("color", color, Field.Store.YES));
            d.Add(new Int32Field("NUM", 11, Field.Store.YES));
            dataList.Add(d);


            return dataList.ToArray();
        }

        public Document[] CreateData()
        {
            List<Document> dataList = new List<Document>();
            for (int i = 0; i < _documentSize; i++)
            {
                string color = (i % 2 == 0) ? "red" : "green";
                string ID = Convert.ToString(i);
                Document d = new Document();
                d.Add(new StringField("id", ID, Field.Store.YES));
                d.Add(new StringField("color", color, Field.Store.YES));
                dataList.Add(d);
            }

            return dataList.ToArray();
        }

        private Directory CreateIndexTwo()
        {
            Directory dir = new RAMDirectory();

            Document[] data = CreateDataTwo();

            TestDataDigester testDigester = new TestDataDigester(_facetHandlers, data);
            BoboIndexer indexer = new BoboIndexer(testDigester, dir);
            indexer.Index();
            using (DirectoryReader r = DirectoryReader.Open(dir))
            { }

            return dir;
        }

        private Directory CreateIndex()
        {
            Directory dir = new RAMDirectory();

            Document[] data = CreateData();

            TestDataDigester testDigester = new TestDataDigester(_facetHandlers, data);
            BoboIndexer indexer = new BoboIndexer(testDigester, dir);
            indexer.Index();
            using (DirectoryReader r = DirectoryReader.Open(dir))
            { }

            return dir;
        }

        public static List<IFacetHandler> CreateFacetHandlers()
        {
            List<IFacetHandler> facetHandlers = new List<IFacetHandler>();
            facetHandlers.Add(new SimpleFacetHandler("id"));
            facetHandlers.Add(new SimpleFacetHandler("color"));
            IFacetHandler rangeFacetHandler = new RangeFacetHandler("idRange", "id", null); //, Arrays.asList(_idRanges));
            facetHandlers.Add(rangeFacetHandler);

            return facetHandlers;
        }

        [Test]
        public void TestNotValuesForSimpleFacetHandler()
        {
            BrowseRequest br = new BrowseRequest();
            br.Count = 20;
            br.Offset = 0;

            BrowseSelection colorSel = new BrowseSelection("color");
            colorSel.AddValue("red");
            br.AddSelection(colorSel);

            BrowseSelection idSel = new BrowseSelection("id");
            idSel.AddNotValue("0");
            br.AddSelection(idSel);

            BrowseResult result = null;
            BoboBrowser boboBrowser = null;
            int expectedHitNum = (_documentSize / 2) - 1;

            using (Directory ramIndexDir = CreateIndex())
            {
                using (DirectoryReader srcReader = DirectoryReader.Open(ramIndexDir))
                {
                    using (boboBrowser = new BoboBrowser(BoboMultiReader.GetInstance(srcReader, _facetHandlers)))
                    {
                        result = boboBrowser.Browse(br);

                        Assert.AreEqual(expectedHitNum, result.NumHits);

                        StringBuilder buffer = new StringBuilder();
                        BrowseHit[] hits = result.Hits;

                        for (int i = 0; i < hits.Length; ++i)
                        {
                            int expectedID = (i + 1) * 2;
                            Assert.AreEqual(expectedID, int.Parse(hits[i].GetField("id")));
                            if (i != 0)
                            {
                                buffer.Append('\n');
                            }
                            buffer.Append("id=" + hits[i].GetField("id") + "," + "color=" + hits[i].GetField("color"));
                        }
                        log.Info(buffer.ToString());
                    }
                }
            }
        }

        [Test]
        public void TestNotValuesForRangeFacetHandler()
        {
            Console.WriteLine("TestNotValuesForRangeFacetHandler");
            BrowseResult result = null;
            BoboBrowser boboBrowser=null;

            using (Directory ramIndexDir = CreateIndexTwo())
            {

                using (DirectoryReader srcReader = DirectoryReader.Open(ramIndexDir))
                {

                    using (boboBrowser = new BoboBrowser(BoboMultiReader.GetInstance(srcReader, _facetHandlers)))
                    {

                        BrowseRequest br = new BrowseRequest();
                        br.Count = (20);
                        br.Offset = (0);

                        if (_idRanges == null)
                        {
                            log.Error("_idRanges cannot be null in order to test NOT on RangeFacetHandler");
                        }
                        BrowseSelection idSel = new BrowseSelection("idRange");
                        idSel.AddNotValue(_idRanges[0]);
                        int expectedHitNum = 1;
                        br.AddSelection(idSel);
                        BooleanQuery q = new BooleanQuery();
                        q.Add(NumericRangeQuery.NewInt32Range("NUM", 10, 10, true, true), Occur.MUST_NOT);
                        q.Add(new MatchAllDocsQuery(), Occur.MUST);
                        br.Query = q;

                        result = boboBrowser.Browse(br);

                        Assert.AreEqual(expectedHitNum, result.NumHits);
                        for (int i = 0; i < result.NumHits; i++)
                        {
                            Console.WriteLine(result.Hits[i]);
                        }
                    }
                }
            }
        }
    }
}
