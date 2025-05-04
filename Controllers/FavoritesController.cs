using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetRefreshTokenDemo.Api.Models;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FavoritesController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetFavorites([FromQuery] string mediaType = null)
    {
        var username = User.Identity.Name;
        var user = await _userManager.FindByNameAsync(username);
        
        if (user == null)
            return NotFound("User not found");
        
        var query = _context.Favorites.Where(f => f.UserId == user.Id);
        
       
        if (!string.IsNullOrEmpty(mediaType))
        {
            query = query.Where(f => f.MediaType == mediaType.ToLower());
        }
        
        var favorites = await query
            .OrderByDescending(f => f.AddedOn)
            .ToListAsync();
            
        return Ok(favorites);
    }

    [HttpPost("add")]
    public async Task<IActionResult> AddFavorite([FromBody] AddFavoriteModel model)
    {
        if (string.IsNullOrEmpty(model.MediaType) || (model.MediaType != "movie" && model.MediaType != "tv"))
            return BadRequest("MediaType must be either 'movie' or 'tv'");
            
        var username = User.Identity.Name;
        var user = await _userManager.FindByNameAsync(username);
        
        if (user == null)
            return NotFound("User not found");
            
       
        var existing = await _context.Favorites
            .FirstOrDefaultAsync(f => 
                f.UserId == user.Id && 
                f.MediaId == model.MediaId && 
                f.MediaType == model.MediaType);
            
        if (existing != null)
            return BadRequest("Media already in favorites");
            
        var favorite = new FavoriteMedia
        {
            UserId = user.Id,
            MediaId = model.MediaId,
            MediaType = model.MediaType,
            Title = model.Title,
            PosterPath = model.PosterPath,
            AddedOn = DateTime.UtcNow
        };
        
        _context.Favorites.Add(favorite);
        await _context.SaveChangesAsync();
        
        return Ok(favorite);
    }

    [HttpDelete("remove")]
    public async Task<IActionResult> RemoveFavorite([FromQuery] int mediaId, [FromQuery] string mediaType)
    {
        if (string.IsNullOrEmpty(mediaType) || (mediaType != "movie" && mediaType != "tv"))
            return BadRequest("MediaType must be either 'movie' or 'tv'");
            
        var username = User.Identity.Name;
        var user = await _userManager.FindByNameAsync(username);
        
        if (user == null)
            return NotFound("User not found");
            
        var favorite = await _context.Favorites
            .FirstOrDefaultAsync(f => 
                f.UserId == user.Id && 
                f.MediaId == mediaId && 
                f.MediaType == mediaType);
            
        if (favorite == null)
            return NotFound("Media not in favorites");
            
        _context.Favorites.Remove(favorite);
        await _context.SaveChangesAsync();
        
        return Ok();
    }
    
    [HttpGet("check")]
    public async Task<IActionResult> CheckFavorite([FromQuery] int mediaId, [FromQuery] string mediaType)
    {
        if (string.IsNullOrEmpty(mediaType) || (mediaType != "movie" && mediaType != "tv"))
            return BadRequest("MediaType must be either 'movie' or 'tv'");
            
        var username = User.Identity.Name;
        var user = await _userManager.FindByNameAsync(username);
        
        if (user == null)
            return NotFound("User not found");
            
        var isFavorite = await _context.Favorites
            .AnyAsync(f => 
                f.UserId == user.Id && 
                f.MediaId == mediaId && 
                f.MediaType == mediaType);
            
        return Ok(new { isFavorite });
    }
}