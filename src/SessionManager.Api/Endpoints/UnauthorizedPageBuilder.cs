namespace SessionManager.Api.Endpoints;

public static class UnauthorizedPageBuilder
{
    public static string BuildUnauthorizedPage()
    {
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
            justify-content: center;
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 50%, #0f3460 100%);
            color: #e4e4e4;
            padding: 2rem;
        }}
        .content {{
            text-align: center;
            max-width: 500px;
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
            font-size: 1.2rem;
            line-height: 1.6;
            color: #e4e4e4;
            margin-bottom: 2rem;
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
        <p class='message'>You need to log in to access this application.</p>
        <a href='https://session-manager.lab.josnelihurt.me/' class='btn'>Go to Login</a>
    </div>
</body>
</html>";
    }
}
