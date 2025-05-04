namespace NetRefreshTokenDemo.Api.Models.DTOs;

public class AddToWatchListModel
{
    public int MediaId { get; set; }
    public string MediaType { get; set; }
    public string Title { get; set; }
    public string PosterPath { get; set; }
}