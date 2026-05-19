using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

[Route("api/[controller]")]
[ApiController]
public class MapController : ControllerBase
{
    private List<Point> _points = new List<Point>();

    [HttpPost("addPoint")]
    public IActionResult AddPoint([FromBody] Point point)
    {
        if (point == null || string.IsNullOrEmpty(point.Name) || point.Coordinates == null)
            return BadRequest("Invalid point data.");

        _points.Add(point);
        return Ok(new { message = "Point added successfully.", pointsCount = _points.Count });
    }

    [HttpGet("getPoints")]
    public IActionResult GetPoints()
    {
        return Ok(_points);
    }
}

public class Point
{
    public string Name { get; set; }
    public double[] Coordinates { get; set; }
}