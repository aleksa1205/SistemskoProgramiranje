﻿using NYTimes_HTTPServer;

HTTPServer server=new HTTPServer();
server.Start();
Console.WriteLine("Press Enter to stop the server...");
while (Console.ReadKey().Key != ConsoleKey.Enter)
server.Stop();
Console.WriteLine("Server stopped!");