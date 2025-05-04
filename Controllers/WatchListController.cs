using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetRefreshTokenDemo.Api.Models;
using NetRefreshTokenDemo.Api.Models.DTOs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NetRefreshTokenDemo.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WatchListController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public WatchListController(AppDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> GetWatchList([FromQuery] string mediaType = null)
        {
            var username = User.Identity.Name;
            var user = await _userManager.FindByNameAsync(username);
            
            if (user == null)
                return NotFound("User not found");
            
            var query = _context.WatchList.Where(w => w.UserId == user.Id);
            
            if (!string.IsNullOrEmpty(mediaType))
            {
                query = query.Where(w => w.MediaType == mediaType.ToLower());
            }
            
            var watchList = await query
                .OrderByDescending(w => w.AddedOn)
                .ToListAsync();
                
            return Ok(watchList);
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToWatchList([FromBody] AddToWatchListModel model)
        {
            if (string.IsNullOrEmpty(model.MediaType) || (model.MediaType != "movie" && model.MediaType != "tv"))
                return BadRequest("MediaType must be either 'movie' or 'tv'");
                
            var username = User.Identity.Name;
            var user = await _userManager.FindByNameAsync(username);
            
            if (user == null)
                return NotFound("User not found");
                
            var existing = await _context.WatchList
                .FirstOrDefaultAsync(w => 
                    w.UserId == user.Id && 
                    w.MediaId == model.MediaId && 
                    w.MediaType == model.MediaType);
                
            if (existing != null)
                return BadRequest("Media already in watch list");
                
            var watchListItem = new WatchListItem
            {
                UserId = user.Id,
                MediaId = model.MediaId,
                MediaType = model.MediaType,
                Title = model.Title,
                PosterPath = model.PosterPath,
                AddedOn = DateTime.UtcNow
            };
            
            _context.WatchList.Add(watchListItem);
            await _context.SaveChangesAsync();
            
            return Ok(watchListItem);
        }

        [HttpDelete("remove")]
        public async Task<IActionResult> RemoveFromWatchList([FromQuery] int mediaId, [FromQuery] string mediaType)
        {
            if (string.IsNullOrEmpty(mediaType) || (mediaType != "movie" && mediaType != "tv"))
                return BadRequest("MediaType must be either 'movie' or 'tv'");
                
            var username = User.Identity.Name;
            var user = await _userManager.FindByNameAsync(username);
            
            if (user == null)
                return NotFound("User not found");
                
            var watchListItem = await _context.WatchList
                .FirstOrDefaultAsync(w => 
                    w.UserId == user.Id && 
                    w.MediaId == mediaId && 
                    w.MediaType == mediaType);
                
            if (watchListItem == null)
                return NotFound("Media not in watch list");
                
            _context.WatchList.Remove(watchListItem);
            await _context.SaveChangesAsync();
            
            return Ok();
        }
        
        [HttpGet("check")]
        public async Task<IActionResult> CheckWatchList([FromQuery] int mediaId, [FromQuery] string mediaType)
        {
            if (string.IsNullOrEmpty(mediaType) || (mediaType != "movie" && mediaType != "tv"))
                return BadRequest("MediaType must be either 'movie' or 'tv'");
                
            var username = User.Identity.Name;
            var user = await _userManager.FindByNameAsync(username);
            
            if (user == null)
                return NotFound("User not found");
                
            var isInWatchList = await _context.WatchList
                .AnyAsync(w => 
                    w.UserId == user.Id && 
                    w.MediaId == mediaId && 
                    w.MediaType == mediaType);
                
            return Ok(new { isInWatchList });
        }
    }
}