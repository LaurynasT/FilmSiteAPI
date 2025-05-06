using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetRefreshTokenDemo.Api.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NetRefreshTokenDemo.Api.Services
{
    public interface IOpenAIService
    {
        Task<RecommendationResponse> GetRecommendationsAsync(List<FavoriteMedia> favorites, string mediaType);
        Task<RecommendationResponse> GetRecommendationsFromTextAsync(string prompt, List<FavoriteMedia> favorites, string mediaType);
    }

    public class OpenAIService : IOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<OpenAIService> _logger;
        private readonly string _apiKey;
        private readonly string _apiEndpoint;
        private readonly string _modelName;

        public OpenAIService(
            IConfiguration configuration,
            HttpClient httpClient,
            ILogger<OpenAIService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;

            _apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger.LogError("OpenAI API key is missing. Please configure it in appsettings.json");
                throw new InvalidOperationException("OpenAI API key is missing");
            }

            _apiEndpoint = _configuration["OpenAI:ApiEndpoint"] ?? "https://api.openai.com/v1/chat/completions";
            _modelName = _configuration["OpenAI:ModelName"] ?? "gpt-3.5-turbo";

            _logger.LogInformation($"OpenAI configured with model: {_modelName}");
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        }

        public async Task<RecommendationResponse> GetRecommendationsAsync(List<FavoriteMedia> favorites, string mediaType)
        {
            if (favorites == null || favorites.Count == 0)
            {
                _logger.LogWarning("No favorites provided for recommendations");
                return new RecommendationResponse
                {
                    Recommendations = new List<MediaRecommendation>(),
                    Message = "Please add some favorites first to get personalized recommendations."
                };
            }

            string mediaTypeDescription = GetMediaTypeDescription(mediaType);

           
            var favoritesList = new StringBuilder();
            foreach (var favorite in favorites.Take(10)) 
            {
                favoritesList.AppendLine($"- {favorite.Title} ({favorite.MediaType})");
            }

            string prompt = $@"Based on these favorites:

{favoritesList}

Recommend 5 similar {mediaTypeDescription} that the user might enjoy. For each recommendation, provide:
1. Title
2. Brief description (1-2 sentences)
3. Why it matches their tastes based on their favorites
4. A score out of 10

IMPORTANT: Format your response as valid JSON with this exact structure:
{{
  ""recommendations"": [
    {{
      ""title"": ""Movie Title"",
      ""description"": ""Brief description"",
      ""reasonForRecommendation"": ""Why it matches their tastes"",
      ""mediaType"": ""movie"",
      ""score"": 8.5
    }},
    // More recommendations...
  ]
}}

Only respond with the JSON, nothing else before or after.";

            return await SendOpenAIRequestAsync(prompt);
        }

        public async Task<RecommendationResponse> GetRecommendationsFromTextAsync(string prompt, List<FavoriteMedia> favorites, string mediaType)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                _logger.LogWarning("Empty prompt provided for text recommendations");
                return new RecommendationResponse
                {
                    Recommendations = new List<MediaRecommendation>(),
                    Message = "Please provide a prompt for recommendations."
                };
            }

            string mediaTypeDescription = GetMediaTypeDescription(mediaType);

            string fullPrompt = $@"
Recommend {mediaTypeDescription} based on the user's input, focusing on their description, genre, and rating preferences. 
Ensure that the recommendations are directly related to the user's criteria and avoid generating unrelated content.

User's request: ""{prompt}""

IMPORTANT RULE: If the request is unrelated to recommending movies or TV shows, respond with this exact message: 
""Sorry, I am not designed to do this.""

For valid requests, provide exactly 5 recommendations. For each recommendation, include:
1. Title
2. Brief description (1-2 sentences)
3. Why it matches the user's criteria
4. A score out of 10

Format the response as valid JSON with this exact structure:
{{
  ""recommendations"": [
    {{
      ""title"": ""Movie Title"",
      ""description"": ""Brief description"",
      ""reasonForRecommendation"": ""Why it matches the user's input"",
      ""mediaType"": ""movie"",
      ""score"": 8.5
    }},
    // More recommendations...
  ]
}}

Only respond with the JSON or the sorry message, nothing else before or after.";

            return await SendOpenAIRequestAsync(fullPrompt);
        }

        private string GetMediaTypeDescription(string mediaType)
        {
            return mediaType?.ToLower() == "movie" ? "movies" :
                   mediaType?.ToLower() == "tv" ? "TV Show" :
                   "movies and TV Show";
        }

        private async Task<RecommendationResponse> SendOpenAIRequestAsync(string prompt)
        {
            try
            {
                var requestData = new
                {
                    model = _modelName,
                    messages = new[]
                    {
                        new { role = "system", content = "You are a helpful assistant that recommends movies and TV shows based on user preferences. You provide thoughtful, personalized recommendations with clear reasons why they match the user's tastes. Always respond with valid JSON in the requested format." },
                        new { role = "user", content = prompt }
                    },
                    temperature = 0.7
                };


                var requestJson = JsonSerializer.Serialize(requestData);
                _logger.LogInformation($"OpenAI API request: {requestJson}");

                var content = new StringContent(
                    JsonSerializer.Serialize(requestData),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(_apiEndpoint, content);


                var responseString = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"Raw OpenAI API response: {responseString}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"OpenAI API error: {response.StatusCode} - {responseString}");
                    return new RecommendationResponse
                    {
                        Recommendations = new List<MediaRecommendation>(),
                        Message = $"Error from recommendation service: {response.StatusCode}. Details: {responseString}"
                    };
                }

                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };

                    var openAIResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseString, options);

                    _logger.LogInformation($"Deserialized response object: {JsonSerializer.Serialize(openAIResponse)}");
                    _logger.LogInformation($"Choices count: {openAIResponse?.Choices?.Count ?? 0}");

                    if (openAIResponse?.Choices == null || openAIResponse.Choices.Count == 0)
                    {
                        _logger.LogError("No choices returned from OpenAI API");
                        return new RecommendationResponse
                        {
                            Recommendations = new List<MediaRecommendation>(),
                            Message = "Failed to get recommendations. Please try again."
                        };
                    }


                    var recommendationsJson = openAIResponse.Choices[0].Message.Content;


                    _logger.LogInformation($"Raw message content before cleaning: {recommendationsJson}");


                    recommendationsJson = CleanJsonResponse(recommendationsJson);


                    _logger.LogInformation($"Cleaned message content: {recommendationsJson}");

                    try
                    {
                        var recommendations = JsonSerializer.Deserialize<RecommendationResponse>(recommendationsJson, options);


                        if (recommendations?.Recommendations == null || recommendations.Recommendations.Count == 0)
                        {
                            _logger.LogWarning("No recommendations returned from OpenAI");
                            return new RecommendationResponse
                            {
                                Recommendations = new List<MediaRecommendation>(),
                                Message = "No recommendations found. Please try again with different criteria."
                            };
                        }

                        return recommendations;
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogError(ex, $"Failed to parse OpenAI response as JSON. Response: {recommendationsJson}");
                        return new RecommendationResponse
                        {
                            Recommendations = new List<MediaRecommendation>(),
                            Message = "Failed to parse recommendations. Please try again."
                        };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, $"JSON deserialization error. Raw response: {responseString}");
                    return new RecommendationResponse
                    {
                        Recommendations = new List<MediaRecommendation>(),
                        Message = "Error processing API response. Please try again."
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling OpenAI API");
                return new RecommendationResponse
                {
                    Recommendations = new List<MediaRecommendation>(),
                    Message = "An error occurred while getting recommendations. Please try again later."
                };
            }
        }

        private string CleanJsonResponse(string jsonResponse)
        {

            if (jsonResponse.StartsWith("```json"))
            {
                jsonResponse = jsonResponse.Replace("```json", "").Replace("```", "").Trim();
            }
            else if (jsonResponse.StartsWith("```"))
            {
                jsonResponse = jsonResponse.Substring(3);
                int endIndex = jsonResponse.IndexOf("```");
                if (endIndex >= 0)
                {
                    jsonResponse = jsonResponse.Substring(0, endIndex).Trim();
                }
                else
                {
                    jsonResponse = jsonResponse.Replace("```", "").Trim();
                }
            }


            int jsonStart = jsonResponse.IndexOf('{');
            if (jsonStart > 0)
            {
                jsonResponse = jsonResponse.Substring(jsonStart);
            }


            int jsonEnd = jsonResponse.LastIndexOf('}');
            if (jsonEnd < jsonResponse.Length - 1 && jsonEnd >= 0)
            {
                jsonResponse = jsonResponse.Substring(0, jsonEnd + 1);
            }

            return jsonResponse;
        }
    }

    public class OpenAIResponse
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public string Model { get; set; }
        public List<OpenAIChoice> Choices { get; set; }
        public OpenAIUsage Usage { get; set; }
    }

    public class OpenAIChoice
    {
        public int Index { get; set; }
        public OpenAIMessage Message { get; set; }
        public string FinishReason { get; set; }
    }

    public class OpenAIMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class OpenAIUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public class RecommendationResponse
    {
        public List<MediaRecommendation> Recommendations { get; set; } = new List<MediaRecommendation>();
        public string Message { get; set; }
    }

    public class MediaRecommendation
    {
        public string Id {get; set;}
        public string Title { get; set; }
        public string Description { get; set; }
        public string ReasonForRecommendation { get; set; }
        public string MediaType { get; set; }

        public decimal Score { get; set; }
    }
}