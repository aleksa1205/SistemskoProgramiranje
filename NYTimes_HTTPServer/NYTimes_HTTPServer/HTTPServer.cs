﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace NYTimes_HTTPServer;

public class HTTPServer
{
    private readonly HttpListener _httpListener;
    private readonly Thread _listenerThread;
    private bool _running;

    public HTTPServer(string address="localhost", int port = 5050)
    {
        _httpListener = new HttpListener();
        _httpListener.Prefixes.Add($"http://{address}:{port}/");
        _listenerThread = new Thread(Listen);
        _running = false;
    }

    private static void SendResponse(HttpListenerContext context, byte[] responseBody, string contentType, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var logString =
            $"REQUEST:\n{context.Request.HttpMethod} {context.Request.RawUrl} HTTP/{context.Request.ProtocolVersion}\n" +
            $"Host: {context.Request.UserHostName}\nUser-agent: {context.Request.UserAgent}\n-------------------\n" +
            $"RESPONSE:\nStatus: {statusCode}\nDate: {DateTime.Now}\nContent-Type: {contentType}" +
            $"\nContent-Length: {responseBody.Length}\n";
        context.Response.ContentType = contentType;
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentLength64 = responseBody.Length;
        using (Stream outputStream = context.Response.OutputStream)
        {
            outputStream.Write(responseBody, 0, responseBody.Length);
        }
        Console.WriteLine(logString);
    }

    private async void AcceptConnection(HttpListenerContext context)
    {
        var request = context.Request;
        if (request.HttpMethod != "GET")
        {
            SendResponse(context, "Method not allowed!"u8.ToArray(), "text/plain", HttpStatusCode.MethodNotAllowed);
            return;
        }

        try
        {
            ApiService apiService = new ApiService();
            string url = request.RawUrl!;
            if (url == String.Empty)
            {
                SendResponse(context, "No name and surname given!"u8.ToArray(), "text/plain", HttpStatusCode.BadRequest);
                return;
            }

            var first = url!.IndexOf("_");
            var name = url.Substring(1, first - 1);
            var surname = url.Substring(first + 1);
            if(!name.All(Char.IsLetter) && !surname.All(Char.IsLetter))
            {
                SendResponse(context, "Name and surname can only contain letters!"u8.ToArray(), "text/plain", HttpStatusCode.UnprocessableContent);
                return;
            }

            List<string> dataTitles = await apiService.FetchData(name, surname);
            if (dataTitles == null)
            {
                SendResponse(context, "API returned an error!"u8.ToArray(), "text/plain", HttpStatusCode.InternalServerError);
            }
            if (dataTitles!.Count == 0)
            {
                SendResponse(context, "No data for given name and surname found!"u8.ToArray(), "text/plain", HttpStatusCode.NoContent);
                return;
            }
            string tmp = String.Join(Environment.NewLine, dataTitles);
            byte[] dataAsBytes = System.Text.Encoding.UTF8.GetBytes(tmp);
            SendResponse(context, dataAsBytes, "text/plain");
        }
        catch(HttpRequestException)
        {
            SendResponse(context, "API returned an error!"u8.ToArray(), "text/plain", HttpStatusCode.InternalServerError);
        }
        catch(Exception)
        {
            SendResponse(context, "Unknown error!"u8.ToArray(), "text/plain", HttpStatusCode.InternalServerError);
        }
    }

    public void Start()
    {
        _httpListener.Start();
        _listenerThread.Start();
        _running = true;
        Console.WriteLine("Server started!");
    }
    
    public void Stop()
    {
        _httpListener.Stop();
        _listenerThread.Join();
        _running = false;
        Console.WriteLine("Server stopped!");
    }
    
    private void Listen()
    {
        while (_running)
        {
            try
            {
                var context = _httpListener.GetContext();
                if (_running)
                {
                    ThreadPool.QueueUserWorkItem(state =>
                    {
                        AcceptConnection(context);
                    });
                }

            }
            catch (HttpListenerException)
            {
                Console.WriteLine("Server stopped listening!");
            }
        }
    }
}
