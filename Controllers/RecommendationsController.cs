using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetRefreshTokenDemo.Api.Models;
using NetRefreshTokenDemo.Api.Services;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace NetRefreshTokenDemo.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RecommendationsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IOpenAIService _openAIService;
        private readonly IMediaLookupService _mediaLookupService; // New service for looking up media IDs

        public RecommendationsController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            IOpenAIService openAIService,
            IMediaLookupService mediaLookupService)
        {
            _context = context;
            _userManager = userManager;
            _openAIService = openAIService;
            _mediaLookupService = mediaLookupService;
        }

        [HttpGet]
        public async Task<IActionResult> GetRecommendations([FromQuery] string mediaType = null)
        {
            var username = User.Identity.Name;
            var user = await _userManager.FindByNameAsync(username);

            if (user == null)
                return NotFound("User not found");
            
            var query = _context.Favorites.Where(f => f.UserId == user.Id);

            if (!string.IsNullOrEmpty(mediaType))
            {
                if (mediaType.ToLower() != "movie" && mediaType.ToLower() != "tv")
                    return BadRequest("MediaType must be either 'movie' or 'tv'");

                query = query.Where(f => f.MediaType == mediaType.ToLower());
            }

            var favorites = await query
                .OrderByDescending(f => f.AddedOn)
                .Take(10)
                .ToListAsync();

            if (favorites.Count == 0)
                return Ok(new { message = "Add some favorites first to get recommendations" });
            
            var recommendations = await _openAIService.GetRecommendationsAsync(favorites, mediaType);
            
            // Enhance recommendations with IDs
            await EnhanceRecommendationsWithIds(recommendations, mediaType);

            return Ok(recommendations);
        }

        [HttpPost("text")]
        public async Task<IActionResult> GetRecommendationsByText([FromBody] TextRecommendationRequest request)
        {
            if (string.IsNullOrEmpty(request.Prompt))
                return BadRequest("Prompt cannot be empty");

            var username = User.Identity.Name;
            var user = await _userManager.FindByNameAsync(username);

            if (user == null)
                return NotFound("User not found");

            var recommendations = await _openAIService.GetRecommendationsFromTextAsync(
                request.Prompt,
                null,
                string.IsNullOrEmpty(request.MediaType) ? null : request.MediaType
            );

            // Enhance recommendations with IDs
            await EnhanceRecommendationsWithIds(recommendations, request.MediaType);

            return Ok(recommendations);
        }

        private async Task EnhanceRecommendationsWithIds(RecommendationResponse recommendations, string mediaType)
        {
            if (recommendations?.Recommendations == null || recommendations.Recommendations.Count == 0)
                return;

            foreach (var recommendation in recommendations.Recommendations)
            {
                
                var mediaId = await _mediaLookupService.LookupMediaIdAsync(
                    recommendation.Title,
                    string.IsNullOrEmpty(mediaType) ? null : mediaType
                );

                
                recommendation.Id = mediaId;
            }
        }
    }

    public class TextRecommendationRequest
    {
        [Required]
        public string Prompt { get; set; }
        
        public string? MediaType { get; set; }
    }
}