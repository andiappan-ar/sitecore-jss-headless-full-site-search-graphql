using GraphQL.Types;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.Linq;
using Sitecore.ContentSearch.Linq.Utilities;
using Sitecore.ContentSearch.Utilities;
using Sitecore.Data;
using Sitecore.Data.Managers;
using Sitecore.Globalization;
using Sitecore.Services.GraphQL.Content.GraphTypes.ContentSearch;
using Sitecore.Services.GraphQL.GraphTypes.Connections;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Sitecore.Foundation.Index.GQLCustomization
{
    public class ExtendedSearchQuery : Sitecore.Services.GraphQL.Content.Queries.SearchQuery
    {
        /// <summary>
        /// Default Search query is extended with new field fieldsContains
        /// </summary>
        public ExtendedSearchQuery()
        {

            this.Arguments.Add(new QueryArgument<ListGraphType<ItemSearchFieldQueryValueGraphType>>()
            {
                Name = "fieldsContains",
                Description = "Filter by index field value using equality (multiple fields are ANDed)"
            });

            this.Arguments.Add(new QueryArgument<BooleanGraphType>()
            {
                Name = "isGlobalSearch",
                Description = "Specify true or false",
                DefaultValue = false
            });

        }

        protected override ContentSearchResults Resolve(ResolveFieldContext context)
        {
            #region Search Query Results
            var inputPathOrIdOrShortId = context.GetArgument<string>("rootItem");
            var keywordArg = context.GetArgument<string>("keyword");
            var nullable1 = context.GetArgument("version", new bool?());
            var flag = !nullable1.HasValue || nullable1.GetValueOrDefault();
            var indexName = context.GetArgument<string>("index");

            ID rootId = null;

            var fieldsContains = context.GetArgument("fieldsContains", new object[0]).OfType<Dictionary<string, object>>();
            bool? isGlobalSearch = context.GetArgument<bool>("isGlobalSearch");
            var facets = (IEnumerable<string>)(context.GetArgument<IEnumerable<string>>("facetOn") ?? new string[0]);

            if (!string.IsNullOrWhiteSpace(inputPathOrIdOrShortId) && Sitecore.Services.GraphQL.Content.GraphTypes.IdHelper.TryResolveItem(this.Database, inputPathOrIdOrShortId, out var result1))
            {
                rootId = result1.ID;
            }

            if (!Language.TryParse(context.GetArgument<string>("language") ?? Sitecore.Context.Language.Name ?? LanguageManager.DefaultLanguage.Name, out var result2))
            {
                result2 = null;
            }

            ///Validate Index Name
            ///Check if the index name as string replacement value "{database}"
            var index = !string.IsNullOrWhiteSpace(indexName)
            ? indexName.Contains("{database}") ? ContentSearchManager.GetIndex(indexName.Replace("{database}", this.Database.Name.ToLower()))
            : ContentSearchManager.GetIndex(indexName)
            : ContentSearchManager.GetIndex($"sitecore_{this.Database.Name.ToLower()}_index");

            using (var searchContext = index.CreateSearchContext())
            {
                var queryable = searchContext.GetQueryable<ContentSearchResult>();

                if (rootId != (ID)null)
                    queryable = queryable.Where(result => result.AncestorIDs.Contains(rootId));

                if (!string.IsNullOrWhiteSpace(keywordArg))
                {
                    #region To avoid duplicate search results for Page URLS
                    ///search the keyword from computed field 
                    if (isGlobalSearch.HasValue && isGlobalSearch == true)
                    {
                        string lowerCasedKeyword = keywordArg?.ToLower();
                        var predicateBuilder = PredicateBuilder.False<ContentSearchResult>();                       
                        predicateBuilder = predicateBuilder.Or(p => p["globalsearchcontent_s"].Contains(lowerCasedKeyword) || p["_name"].Contains(keywordArg));                       
                        queryable = queryable.Where(predicateBuilder);

                    }
                    #endregion
                    else
                    {
                        queryable = queryable.Where(result => result.Content.Contains(keywordArg));
                    }

                }
                if (result2 != null)
                {
                    var resultLanguage = result2.Name;
                    queryable = queryable.Where(result => result.Language == resultLanguage);
                }

                if (flag)
                    queryable = queryable.Where(result => result.IsLatestVersion);

                ///Field Contains checks the field and value sent and filter the items out
                foreach (var dictionary in fieldsContains)
                {
                    var name = dictionary["name"].ToString();
                    var value = dictionary["value"].ToString();
                    if (value.Contains("contains:"))
                    {
                        var parts = value.Split(':');
                        var values = parts[1].Split(',');

                        var predicateBuilder = PredicateBuilder.False<ContentSearchResult>();
                        values.ForEach(
                            v => predicateBuilder = predicateBuilder.Or(result => result[name].Contains(v)));

                        queryable = queryable.Where(predicateBuilder);
                    }
                    else
                    {
                        queryable = queryable.Where(result => result[name].Contains(value));
                    }
                }

                foreach (var str in facets)
                {
                    var facet = str;
                    queryable = queryable.FacetOn(result => result[facet]);
                }

                var nullable2 = context.GetArgument("after", new int?());


                #region Final Queried Results
                var results = new ContentSearchResults(
                   queryable.ApplyEnumerableConnectionArguments<ContentSearchResult, object>(context).GetResults(),
                   nullable2 ?? 0);


                return results;
                #endregion


            }

        }

        #endregion
    }
}