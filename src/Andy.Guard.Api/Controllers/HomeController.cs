using Microsoft.AspNetCore.Mvc;

namespace Andy.Guard.Controllers;

/// <summary>
/// Home controller that provides the main landing page and basic information endpoints.
/// </summary>
[ApiController]
[Route("")]
public class HomeController : ControllerBase
{
    /// <summary>
    /// Returns the home page with application status information.
    /// </summary>
    /// <returns>HTML content for the home page.</returns>
    [HttpGet("")]
    public IActionResult Index()
    {
        var html = @"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Andy Guard - Home</title>
    <style>
        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            margin: 0;
            padding: 0;
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            min-height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
        }
        .container {
            background: white;
            padding: 3rem;
            border-radius: 10px;
            box-shadow: 0 20px 60px rgba(0,0,0,0.1);
            text-align: center;
            max-width: 600px;
        }
        h1 {
            color: #333;
            margin-bottom: 1rem;
        }
        p {
            color: #666;
            line-height: 1.6;
            margin-bottom: 2rem;
        }
        .links {
            display: flex;
            gap: 1rem;
            justify-content: center;
            flex-wrap: wrap;
        }
        a {
            display: inline-block;
            padding: 0.75rem 1.5rem;
            background: #667eea;
            color: white;
            text-decoration: none;
            border-radius: 5px;
            transition: background 0.3s;
        }
        a:hover {
            background: #764ba2;
        }
        .status {
            margin-top: 2rem;
            padding: 1rem;
            background: #f0f9ff;
            border-left: 4px solid #3b82f6;
            text-align: left;
        }
        .status-title {
            font-weight: bold;
            color: #1e40af;
            margin-bottom: 0.5rem;
        }
        .status-item {
            color: #64748b;
            font-size: 0.9rem;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>Welcome to Andy Guard</h1>
        <p>
            Your ASP.NET Core application is up and running! This is a modern web API built with .NET 8,
            ready to power your next great idea.
        </p>
        
        <div class='links'>
            <a href='/swagger'>API Documentation</a>
            <a href='/weatherforecast'>Weather API</a>
        </div>
        
        <div class='status'>
            <div class='status-title'>Application Status</div>
            <div class='status-item'>✅ Server is running</div>
            <div class='status-item'>✅ API endpoints are active</div>
            <div class='status-item'>✅ Swagger documentation available</div>
        </div>
    </div>
</body>
</html>";

        return Content(html, "text/html");
    }
}
