# sitecore-jss-headless-full-site-search-graphql non-edge

This extention helps you to implement full text site search using graphQL in sitecore.
Complete article available here : https://andisitecore.wordpress.com/2023/11/08/full-text-site-search-with-graphql-non-edge/

## What it covers
1. Return a page: The search results should return the actual page, not just the data source item. For instance, if a user searches for a keyword, the results should display the page itself, not the respective data source item.
   
2. Search with sub-components: When implementing search with sub-components, the goal is to generate a list of pages containing the keyword that the user is searching for. This list should include the pages and their associated components. For instance, if the Hero Banner component contains specific text and a user searches with that specific text, the search results should include the pages associated with this component.
   
3. PDF/Word documents: The search results should also take into account media files. For instance, if the keyword is present within a PDF or Word document, the search results should include this media file.
   
4. Multiple word search: The search should support multiple keyword searches. For example, if a user types ‘word1 word2,’ the search results should consider both of these values in the search results.

## Solution with the graphQL

* We will create a computed index field named ‘GlobalSearchContent’ that will aggregate the content from all subcomponents on every page of the website.
* Then, we will implement a custom GraphQL extender named ‘extendsearch.’ With this extender, we can perform keyword searches against the ‘GlobalSearchContent’ computed index field.
* Finally ensuring that the search results return complete page lists.
* Additionally, we will include media files in the computed index field for a more comprehensive search experience.

![image](https://github.com/andiappan-ar/sitecore-jss-headless-full-site-search-graphql/assets/11770345/f8346de7-52cc-4407-8f1e-23770e5e8b54)

## Test the things on the playground
Go to your playground and test it.

For demo purposes, let me add a specific word in the home page hero banner component.
![image](https://github.com/andiappan-ar/sitecore-jss-headless-full-site-search-graphql/assets/11770345/17574046-dd91-4982-a08b-dfae102bf52b)


Let’s test with OTB grpahQL search query, See it returns data source instead of the actual page.
![image](https://github.com/andiappan-ar/sitecore-jss-headless-full-site-search-graphql/assets/11770345/a00f2c98-5592-4ec1-b179-17f7a6d81886)


Now lets run with our custom extended search query,

It’s giving the expected result as a proper page.

![image](https://github.com/andiappan-ar/sitecore-jss-headless-full-site-search-graphql/assets/11770345/8d0caf20-4636-4e31-aec7-72eddaea324b)

