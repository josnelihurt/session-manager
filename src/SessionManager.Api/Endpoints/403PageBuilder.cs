namespace SessionManager.Api.Endpoints;

public static class ForbiddenPageBuilder
{
    private static readonly string[] ForbiddenMessages =
    [
        "Thou shall not pass!",
        "The magical seal on this door is too powerful.",
        "The griffin at the gate has rejected thy credentials.",
        "The Royal Wizard Council has not granted thee permission.",
        "By the power of Grayskull... actually, thou just don't have access.",
        "The drawbridge is up.",
        "The crystal ball shows that thou art not on the guest list.",
        "The enchanted forest path is blocked.",
        "The dragons guarding this domain have been alerted.",
        "The Book of Forbidden Knowledge says: 'Access denied.'",
        "The knights have voted against thy admission.",
        "The alchemist's potion of 'access granted' has failed.",
        "The oracle has spoken: 'Thou shalt not pass.'",
        "The magical wards repel all unauthorized visitors.",
        "The castle scribe has no record of thy name.",
        "The enchanted shield blocks thy path.",
        "The fairy at the entrance has decreed: 'None shall pass.'",
        "The dungeon master says: 'Thou hast not the required level.'",
        "The magical mirror shows only those with authorization may enter.",
        "The ancient runes translate to: 'Access Denied.'"
    ];

    public static string BuildForbiddenPage(string? appName = null)
    {
        var random = new Random();
        var messageIndex = random.Next(ForbiddenMessages.Length);

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>403 - Forbidden</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: flex-end;
            background-image: url('https://session-manager.lab.josnelihurt.me/api/static/403.png');
            background-size: cover;
            background-position: center;
            background-repeat: no-repeat;
            padding: 2rem;
            padding-bottom: 4rem;
        }}
        .content {{
            text-align: center;
            max-width: 600px;
        }}
        h1 {{
            font-size: 3rem;
            font-weight: 700;
            color: #ffd700;
            text-shadow: 2px 2px 8px rgba(0, 0, 0, 0.8),
                         0 0 30px rgba(255, 215, 0, 0.4);
            margin-bottom: 1.5rem;
        }}
        .message {{
            font-size: 1.5rem;
            line-height: 1.6;
            color: #fff;
            text-shadow: 2px 2px 6px rgba(0, 0, 0, 0.9);
            margin-bottom: 2rem;
            font-style: italic;
        }}
        .btn {{
            display: inline-block;
            padding: 1rem 2rem;
            background: rgba(255, 215, 0, 0.95);
            color: #1a1a2e;
            text-decoration: none;
            border-radius: 8px;
            font-size: 1.1rem;
            font-weight: 600;
            transition: all 0.3s ease;
            box-shadow: 0 4px 15px rgba(0, 0, 0, 0.5);
        }}
        .btn:hover {{
            background: rgba(255, 235, 59, 1);
            transform: translateY(-2px);
            box-shadow: 0 6px 20px rgba(0, 0, 0, 0.6);
        }}
    </style>
</head>
<body>
    <div class='content'>
        <h1>403 - Forbidden</h1>
        <p class='message'>{ForbiddenMessages[messageIndex]}</p>
        <a href='https://josnelihurt.me' class='btn'>Return to josnelihurt.me</a>
    </div>
</body>
</html>";
    }
}
