using System;
using System.Collections.Generic;
using System.IO;
using HTTProof.Models;

namespace HTTProof.Services;

public sealed class CliOptions
{
    public string? Method { get; private set; }
    public string? Url { get; private set; }
    public List<(string, string)> Params { get; } = new();
    public List<(string, string)> Headers { get; } = new();
    public AuthKind? Auth { get; private set; }
    public string? AuthUser { get; private set; }
    public string? AuthSecret { get; private set; }
    public BodyKind? Body { get; private set; }
    public string? BodyText { get; private set; }
    public bool Send { get; private set; }
    public const int DefaultPort = 5959;
    public int Port { get; private set; } = DefaultPort;
    public bool Help { get; private set; }
    public string? ParseError { get; private set; }

    public static CliOptions Parse(string[] argv)
    {
        var o = new CliOptions();
        try
        {
            for (var i = 0; i < argv.Length; i++)
            {
                var a = argv[i];
                switch (a)
                {
                    case "--method" or "-X":
                        o.Method = Require(argv, ref i, a).ToUpperInvariant();
                        break;
                    case "--url" or "-u":
                        o.Url = Require(argv, ref i, a);
                        break;
                    case "--param" or "-p":
                        o.Params.Add(SplitKv(Require(argv, ref i, a), '=', a));
                        break;
                    case "--header" or "-H":
                        o.Headers.Add(SplitKv(Require(argv, ref i, a), ':', a));
                        break;
                    case "--auth-basic":
                        {
                            var kv = SplitKv(Require(argv, ref i, a), ':', a);
                            o.Auth = AuthKind.Basic;
                            o.AuthUser = kv.Item1;
                            o.AuthSecret = kv.Item2;
                            break;
                        }
                    case "--auth-bearer":
                        o.Auth = AuthKind.Bearer;
                        o.AuthSecret = Require(argv, ref i, a);
                        break;
                    case "--body-json":
                        o.Body = BodyKind.Json;
                        o.BodyText = ReadAtOr(Require(argv, ref i, a));
                        break;
                    case "--body-text":
                        o.Body = BodyKind.Text;
                        o.BodyText = ReadAtOr(Require(argv, ref i, a));
                        break;
                    case "--send":
                        o.Send = true;
                        break;
                    case "--port":
                        o.Port = int.Parse(Require(argv, ref i, a));
                        break;
                    case "--help" or "-h" or "/?":
                        o.Help = true;
                        break;
                    default:
                        throw new ArgumentException($"unknown arg: {a}");
                }
            }
        }
        catch (Exception ex)
        {
            o.ParseError = ex.Message;
        }
        return o;
    }

    private static string Require(string[] argv, ref int i, string a)
    {
        if (i + 1 >= argv.Length) throw new ArgumentException($"{a} requires a value");
        return argv[++i];
    }

    private static (string, string) SplitKv(string raw, char sep, string flag)
    {
        var idx = raw.IndexOf(sep);
        if (idx < 0) throw new ArgumentException($"{flag} expects key{sep}value, got '{raw}'");
        return (raw[..idx].Trim(), raw[(idx + 1)..].Trim());
    }

    private static string ReadAtOr(string raw)
    {
        if (raw.StartsWith('@'))
        {
            var path = raw[1..];
            return File.ReadAllText(path);
        }
        return raw;
    }

    public static string HelpText() => """
HTTProof - screenshottable REST client, driveable by CLI & local HTTP API.

Usage:
  HTTProof [options]

Request prep:
  --method, -X <VERB>          HTTP method (default GET)
  --url, -u <URL>              Request URL
  --param, -p <k=v>            Query param (repeatable)
  --header, -H <k:v>           Header (repeatable)
  --auth-basic <user:pass>     Basic auth
  --auth-bearer <token>        Bearer token
  --body-json <json|@file>     JSON body (prefix @ for file)
  --body-text <text|@file>     Text body

Behavior:
  --send                       Send the prepared request immediately, print JSON response
  --port <n>                   Bind driving server to specific port (default: 5959; falls back to ephemeral if in use)
  --help, -h                   Show this help

Driving API (once running):
  GET  /api/state              full request+response state
  PUT  /api/state              patch request fields (json body)
  POST /api/send               send now; returns response JSON
  POST /api/exit               close the app
""";
}
