using DocumentFormat.OpenXml.Packaging;
using HtmlAgilityPack;
using iTextSharp.text.pdf;
using iTextSharp.text.pdf.parser;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.ComputedFields;
using Sitecore.ContentSearch.Diagnostics;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Layouts;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Sitecore.Foundation.Index.ComputedFields
{
    /// <summary>
    /// Computed field that contains all textual content of items that are rendering data sources on the current item's layout details
    /// </summary>
    public class GlobalSearchContent : IComputedIndexField
    {
        public object ComputeFieldValue(IIndexable indexable)
        {
            var sitecoreIndexable = indexable as SitecoreIndexableItem;
            List<string> contentToAdd = new List<string>();
            if (sitecoreIndexable == null) return null;

            if (sitecoreIndexable.Item?.Fields["globalsearchindexablefield"]?.Value == "1" && !sitecoreIndexable.Item.Paths.Path.Contains("/templates"))
            {
                // find renderings with datasources set
                var customDataSources = ExtractRenderingDataSourceItems(sitecoreIndexable.Item);

                // extract text from data sources
                contentToAdd = customDataSources.SelectMany(GetItemContent).ToList();

                if (contentToAdd.Count == 0) return null;
            }
            else if (sitecoreIndexable.Item != null && sitecoreIndexable.Item.Paths.IsMediaItem)
            {
                MediaItem _media = sitecoreIndexable.Item;
                string ext = _media.Extension.ToLower();
                if (ext == "pdf" || _media.MimeType == "application/pdf")
                {
                    contentToAdd.Add(ParsePDF(_media));
                }
                else if (ext == "docx" || ext == "doc")
                {
                    contentToAdd.Add(ParseItemsWithIfilters(_media));
                }
            }

            return string.Join(" ", contentToAdd)?.ToLower();
        }

        /// <summary>
        /// Finds all renderings on an item's layout details with valid custom data sources set and returns the data source items.
        /// </summary>
        protected virtual IEnumerable<Item> ExtractRenderingDataSourceItems(Item baseItem)
        {
            string currentLayoutXml = LayoutField.GetFieldValue(baseItem.Fields[FieldIDs.LayoutField]);
            if (string.IsNullOrEmpty(currentLayoutXml)) yield break;

            LayoutDefinition layout = LayoutDefinition.Parse(currentLayoutXml);

            // Evalute each device
            for (int deviceIndex = layout.Devices.Count - 1; deviceIndex >= 0; deviceIndex--)
            {
                var device = layout.Devices[deviceIndex] as DeviceDefinition;

                if (device == null)
                    continue;

                var deviceItem = baseItem.Database.GetItem(ID.Parse(device.ID));

                if (deviceItem == null) continue;

                var renderings = baseItem.Visualization.GetRenderings(deviceItem, false);
                if (renderings == null || renderings.Length == 0)
                    yield break;

                for (var index = renderings.Length - 1; index >= 0; index--)
                {
                    var rendering = renderings[index];

                    if (rendering == null || rendering.Database != baseItem.Database)
                        continue;

                    var dataSourceId = rendering.Settings.DataSource;

                    if (!ID.IsID(dataSourceId))
                        continue;
                    var datasourceItem = baseItem.Database.GetItem(new ID(dataSourceId));

                    if (datasourceItem == null)
                        continue;

                    yield return datasourceItem;
                }
            }


        }

        /// <summary>
        /// Extracts textual content from an item's fields
        /// </summary>
        protected virtual IEnumerable<string> GetItemContent(Item dataSource)
        {
            if (dataSource.Fields["HideInSearchResults"] == null || dataSource.Fields["HideInSearchResults"]?.Value == "0")
            {
                //Checks and adds the parent Item
                var ChildItems =dataSource.HasChildren? GetChildItemsRecursive(dataSource) : new List<Item>() {dataSource};
                foreach (Item datasourceItem in ChildItems)
                {
                    foreach (Field field in datasourceItem.Fields)
                    {
                        // this check is what Sitecore uses to determine if a field belongs in _content (see LuceneDocumentBuilder.AddField())
                        if (!IndexOperationsHelper.IsTextField(new SitecoreItemDataField(field))) continue;

                        string fieldValue = IndexOperationsHelper.StripHtml((field.Value ?? string.Empty));

                        if (!string.IsNullOrWhiteSpace(fieldValue)) yield return fieldValue;
                    }

                }
            }
        }


        // Recursive method to get child items in a nested way
        public static List<Item> GetChildItemsRecursive(Item parentItem)
        {
            List<Item> childItems = new List<Item>();
            if (parentItem != null)
            { 
                //Gets the last childitem and return thus by blocking the duplication
                if (!parentItem.HasChildren)
                {
                    return childItems;
                }
                else
                {// Get the immediate child items of the parent item
                    foreach (Item childItem in parentItem.Children)
                    {
                        childItems.Add(childItem);
                        List<Item> nestedChildItems = GetChildItemsRecursive(childItem); childItems.AddRange(nestedChildItems);
                    }
                }
            }
            return childItems;
        }

        private static string StripHtmlTags(string source)
        {
            if (source == null)
                return null;

            var doc = new HtmlDocument();
            doc.LoadHtml(source);
            return doc.DocumentNode.InnerText;
        }

        private string ParsePDF(MediaItem mediaItem)
        {
            ITextExtractionStrategy strategy = new SimpleTextExtractionStrategy();

            var builder = new StringBuilder();
            if (mediaItem != null)
            {
                try
                {
                    var reader = new PdfReader(mediaItem.GetMediaStream());
                    if (reader.Info.ContainsKey("Title"))
                    {
                        builder.Append(reader.Info["Title"]);
                    }
                    if (reader.Info.ContainsKey("Subject"))
                    {
                        builder.Append(reader.Info["Subject"]);
                    }

                    if (reader.Info.ContainsKey("Keywords"))
                    {
                        builder.Append(reader.Info["Keywords"]);
                    }

                    for (int pagenumber = 1; pagenumber <= reader.NumberOfPages; pagenumber++)
                    {
                        builder.Append(PdfTextExtractor.GetTextFromPage(reader, pagenumber, strategy));
                    }
                }
                catch (Exception ex)
                {
                    CrawlingLog.Log.Error(ex.ToString(), ex);
                    return string.Empty;
                }
            }
            return builder.ToString();
        }


        private string ParseItemsWithIfilters(MediaItem mediaItem)
        {
            string content = string.Empty;
            try
            {
                using (Stream streamReader = mediaItem.GetMediaStream())
                {
                    using (WordprocessingDocument wordDocument = WordprocessingDocument.Open(streamReader, false))
                    {
                        DocumentFormat.OpenXml.Wordprocessing.Body body
                         = wordDocument.MainDocumentPart.Document.Body;
                        content = body.InnerText;
                    }
                }
            }
            catch (Exception ex)
            {
                CrawlingLog.Log.Error(ex.ToString(), ex);
            }

            if (!string.IsNullOrWhiteSpace(content))
            {
                content = content.Replace("\r\n", string.Empty).ToLower();
            }

            return content;
        }

        public string FieldName { get; set; }
        public string ReturnType { get; set; }
    }
}