using System;

namespace NetRefreshTokenDemo.Api.Models;

public class WatchListItem
{
    public int Id { get; set; }
    public string UserId { get; set; }
    public int MediaId { get; set; }
    public string MediaType { get; set; } // "movie" or "tv"
    public string Title { get; set; }
    public string PosterPath { get; set; }
    public DateTime AddedOn { get; set; }
    
    // Navigation property
    public ApplicationUser User { get; set; }
}