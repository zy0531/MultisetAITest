using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class QueryTimer : RealtimeQueryManager
{
    private int CurrentQuery = 0;

    [Header("Query Timer File Setting")]
    public Boolean useFile;
    public string CsvFilePath;
    private List<string> Queries = new();
    private List<string> OutData = new();
    private Boolean QuerySent = false;
    private Boolean SendingQueries = false;
    private float start;
    private float end;
    private string response;
    private Boolean alreadyStarted = false;

    protected override void Start()
    {
        if (!alreadyStarted)
            base.Start();

        alreadyStarted = true;

        if (!useFile)
            return;

        using (StreamReader sr = File.OpenText(CsvFilePath))
        {
            string s;
            while ((s = sr.ReadLine()) != null)
            {
                string[] parts = s.Split(',');
                Queries.Add(parts[0]);
            }
        }

    }

    protected override void Update()
    {
        // If we have a response, assign end.
        // Technically this calls before we cancel if !useFile,
        // but it shouldn't really matter too much imo
        // Also this has to be called before Update since it
        // will consume the response string we're looking for
        if (responseQueue.Count > 0 && SendingQueries)
        {
            end = Time.realtimeSinceStartup;
            responseQueue.TryPeek(out response);
            OutData.Add(
                string.Format("{0},{1},{2}",
                Queries[CurrentQuery],
                end - start, response
                )
            );
            CurrentQuery++;
            QuerySent = false;

            if (CurrentQuery >= Queries.Count - 1)
            {
                using (StreamWriter sw = File.CreateText(CsvFilePath))
                {
                    foreach (string line in OutData)
                        sw.WriteLine(line);
                }
                SendingQueries = false;
            }
        }

        base.Update();

        if (!useFile || !SendingQueries)
            return;
        
        if (!QuerySent)
        {
            start = Time.realtimeSinceStartup;
            SendQuery(Queries[CurrentQuery]);
            QuerySent = true;
        }

    }

    [ContextMenu("Time CSV Queries")]
    public void SendQueries()
    {
        if (Queries.Count == 0)
            Start();
        SendingQueries = true;
    }
}
