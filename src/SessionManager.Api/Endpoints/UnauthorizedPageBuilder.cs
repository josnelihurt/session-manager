namespace SessionManager.Api.Endpoints;

public static class UnauthorizedPageBuilder
{
    private static readonly string[] UnauthorizedMessages =
    [
        "The magical gatekeeper requests thy presence.",
        "The enchanted scroll of authentication is missing.",
        "The wizard's crystal ball shows no record of thee.",
        "The guardian of the realm demands identification.",
        "The mystical seal blocks thy path.",
        "The ancient rune reads: 'Who goes there?'",
        "The castle sentry knows thee not.",
        "The alchemist's potion of 'login' must be consumed first.",
        "The fairy whispers: 'Sign in to proceed.'",
        "The enchanted mirror shows only those logged in may enter.",
        "The royal decree requires authentication.",
        "The dungeon master says: 'Thou hast not logged in yet.'",
        "The magical wards require a session key.",
        "The herald announces: 'Unknown traveler detected.'",
        "The sorcerer's scry shows no valid credentials.",
        "The bridge keeper asks: 'What is your password?'",
        "The mystical portal is sealed.",
        "The oracle whispers: 'Login first, mortal.'",
        "The enchanted forest requires a guide.",
        "The Book of Entry says: 'Authentication required.'"
    ];

    public static string BuildUnauthorizedPage()
    {
        var random = new Random();
        var messageIndex = random.Next(UnauthorizedMessages.Length);

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>401 - Unauthorized</title>
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
        <h1>401 - Unauthorized</h1>
        <p class='message'>{UnauthorizedMessages[messageIndex]}</p>
        <a href='https://session-manager.lab.josnelihurt.me/' class='btn'>Go to Login</a>
    </div>
</body>
</html>";
    }
}
