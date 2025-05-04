using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace NetRefreshTokenDemo.Api.Services
{
    public interface IMediaLookupService
    {
        Task<string> LookupMediaIdAsync(string title, string mediaType);
    }

    public class MediaLookupService : IMediaLookupService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MediaLookupService> _logger;
        private readonly string _apiKey;

        public MediaLookupService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<MediaLookupService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            // Get TMDB API key from configuration
            _apiKey = _configuration["TMDB:ApiKey"];
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning("TMDB API key is missing. ID lookup will not work.");
            }
        }

        public async Task<string> LookupMediaIdAsync(string title, string mediaType)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogWarning($"Unable to look up ID for '{title}' - API key not configured");
                return null;
            }

            try
            {
                // Determine if we're looking for movies or TV shows
                string searchType = mediaType?.ToLower() == "tv" ? "tv" : "movie";

                // Construct the search URL
                string searchUrl = $"https://api.themoviedb.org/3/search/{searchType}?api_key={_apiKey}&query={Uri.EscapeDataString(title)}";

                // Make the request
                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"TMDB API error: {response.StatusCode} when searching for '{title}'");
                    return null;
                }

                // Parse the response
                var content = await response.Content.ReadAsStringAsync();
                var searchResult = JsonSerializer.Deserialize<TMDBSearchResponse>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Check if we found any results
                if (searchResult?.Results == null || searchResult.Results.Count == 0)
                {
                    _logger.LogWarning($"No results found for '{title}'");
                    return null;
                }

                // Return the ID of the first (most relevant) match
                return searchResult.Results[0].Id.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error looking up ID for '{title}'");
                return null;
            }
        }

        private class TMDBSearchResponse
        {
            public int Page { get; set; }
            public List<TMDBSearchResult> Results { get; set; }
            public int TotalResults { get; set; }
            public int TotalPages { get; set; }
        }

        private class TMDBSearchResult
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Name { get; set; } // For TV shows
            // Other properties omitted for brevity
        }
    }
}