﻿using Game.Citizens;
using Game.Companies;
using Game.Simulation;
using Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Game.Assets;
using System.IO;
using Game.Prefabs;
using Colossal.Entities;
using Colossal.IO.AssetDatabase.Internal;
using static System.Net.Mime.MediaTypeNames;
using Colossal.Json;
using TripsData;
using Game.Pathfind;

namespace TripsData
{
    public partial class CitizenStatistics : GameSystemBase
    {
        private Dictionary<Entity, TravelPurpose> _CitizenToData = new Dictionary<Entity, TravelPurpose>();
        private Dictionary<int, Purpose[]> _outputData = new Dictionary<int, Purpose[]>();

        private EntityQuery _query;
        private int previous_index = -1;

        protected override void OnCreate()
        {
            base.OnCreate();

            _query = GetEntityQuery(new EntityQueryDesc()
            {
                All = new[] {
                    ComponentType.ReadWrite<TravelPurpose>()
                }
            });

            RequireForUpdate(_query);
        }

        public override int GetUpdateInterval(SystemUpdatePhase phase)
        {
            // One day (or month) in-game is '262144' ticks
            return TimeSystem.kTicksPerDay / 64;
        }
        protected override void OnUpdate()
        {
            DateTime currentDateTime = this.World.GetExistingSystemManaged<TimeSystem>().GetCurrentDateTime();
            int half_hour = 0;
            if (currentDateTime.Minute >= 30)
            {
                half_hour = 1;
            }
            int index = 2 * currentDateTime.Hour + half_hour;

            if (previous_index != index)
            {
                previous_index = index;
                var citizens = _query.ToEntityArray(Allocator.Temp);

                //Citizen Purposes
                int[] cimpurp = new int[(int)Purpose.Count];

                foreach (var cim in citizens)
                {
                    Citizen data1;
                    TravelPurpose data2;

                    data2 = EntityManager.GetComponentData<TravelPurpose>(cim);
                    if (EntityManager.TryGetComponent<Citizen>(cim, out data1))
                    {
                        Purpose[] purp;
                        if (!_outputData.ContainsKey(cim.Index))
                        {
                            // 0 - 23 YESTERDAY, 24 to 71 TODAY
                            purp = new Purpose[72];
                            purp[index + 24] = data2.m_Purpose;
                            _outputData.Add(cim.Index, purp);
                        }
                        else
                        {
                            if (_outputData.TryGetValue(cim.Index, out purp))
                            {
                                purp[index + 24] = data2.m_Purpose;
                                _outputData[cim.Index] = purp;
                            }
                        }

                        cimpurp[(int)data2.m_Purpose]++;
                    }
                }

                string path = Path.Combine(Mod.outputPath, Mod.cimpurposeoutput);
                string fileName = path +
                    "_" + currentDateTime.DayOfYear + "_" + currentDateTime.Year + ".csv";

                if(Mod.m_Setting.citizen_purpose)
                {
                    if (index == 0)
                    {
                        string header = "hour";
                        for(int i = 0; i < (int)Purpose.Count; i++)
                        {
                            Purpose purps = (Purpose)i;
                            header += $",{purps.ToString()}";
                        }

                        Utils.createAndDeleteFiles(fileName, header, Mod.cimpurposeoutput, path);
                    }

                    string line = $"{(float)index / 2f}";
                    for (int i = 0; i < (int)Purpose.Count; i++)
                    {
                        line += $",{cimpurp[i]}";
                    }

                    using (StreamWriter sw = File.AppendText(fileName))
                    {
                        sw.WriteLine(line);
                    }
                }
                
                if (currentDateTime.Hour == 23 && half_hour == 1)
                {
                    int[] hbw = new int[48];
                    int[] hbsch = new int[48];
                    int[] hbo = new int[48];
                    int[] nhb = new int[48];

                    for (int h = 0; h < 48; h++)
                    {
                        hbw[h] = 0;
                        hbsch[h] = 0;
                        hbo[h] = 0;
                        nhb[h] = 0;
                    }

                    foreach (var key in _outputData.Keys)
                    {
                        Purpose[] purp = _outputData[key];
                        Purpose prev = Purpose.PathFailed;
                        int i = 0;
                        List<Purpose> tpurp = new List<Purpose>();
                        List<int> thour = new List<int>();
                        while (i < 72)
                        {
                            Purpose current = purp[i];

                            if (i < 71)
                            {
                                int j = i + 1;
                                Purpose next = purp[j];

                                while (current.Equals(next) && j < 71)
                                {
                                    j++;
                                    next = purp[j];
                                }

                                if (!current.Equals(next))
                                {
                                    if (!current.Equals(Purpose.None) && !current.Equals(prev))
                                    {
                                        tpurp.Add(current);
                                        thour.Add(i);
                                        prev = current;
                                    }
                                }

                                i = j;
                            }
                            else
                            {
                                if (!current.Equals(purp[i - 1]))
                                {
                                    if (!current.Equals(Purpose.None) && !current.Equals(prev))
                                    {
                                        tpurp.Add(current);
                                        thour.Add(i);
                                        prev = current;
                                    }
                                }
                                i++;
                            }
                        }

                        if (tpurp.Count <= 2)
                        {
                            continue;
                        }

                        for (int k = 0; k < tpurp.Count; k++)
                        {
                            Purpose current = tpurp[k];
                            Purpose previous;
                            Purpose next;

                            int previous_hour;
                            int next_hour;

                            if (k == 0)
                            {
                                previous = tpurp[tpurp.Count - 1];
                                previous_hour = thour[thour.Count - 1];
                            }
                            else
                            {
                                previous = tpurp[k - 1];
                                previous_hour = thour[k - 1];
                            }

                            if (k == (tpurp.Count - 1))
                            {
                                next = tpurp[0];
                                next_hour = thour[0];
                            }
                            else
                            {
                                next = tpurp[k + 1];
                                next_hour = thour[k + 1];
                            }

                            int h = thour[k];
                            int hour_diff = h - previous_hour;
                            if(previous_hour > h)
                            {
                                hour_diff += 72;
                            }
                            if (h > 23 && (hour_diff) < 20)
                            {
                                if ((h == 24 && previous.Equals(next) && (next_hour == 25)) || previous.Equals(current))
                                {
                                    continue;
                                }
                                // Going to Work
                                if (current.Equals(Purpose.GoingToWork))
                                {
                                    if (h == 24 && next_hour == 25 && (next.Equals(Purpose.Sleeping) || (next.Equals(Purpose.GoingHome) && previous.Equals(Purpose.GoingHome))))
                                    {
                                        continue;
                                    }
                                    if (previous.Equals(Purpose.Working))
                                    {
                                        continue;
                                    }
                                    if (previous.Equals(Purpose.Sleeping) || previous.Equals(Purpose.None) || previous.Equals(Purpose.GoingHome))
                                    {
                                        hbw[h - 24]++;
                                    }
                                    else
                                    {
                                        nhb[h - 24]++;
                                    }
                                }
                                else
                                {
                                    // For cases where status is "Working", but there is no "GoingToWork" status before between now and previous location
                                    if (current.Equals(Purpose.Working))
                                    {
                                        if (next.Equals(Purpose.GoingToWork))
                                        {
                                            continue;
                                        }
                                        if (previous.Equals(Purpose.GoingHome) || previous.Equals(Purpose.Sleeping) || previous.Equals(Purpose.None))
                                        {
                                            hbw[h - 24]++;
                                        }
                                        else
                                        {
                                            if (!previous.Equals(Purpose.GoingToWork))
                                            {
                                                nhb[h - 24]++;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (current.Equals(Purpose.GoingToSchool))
                                        {
                                            if (previous.Equals(Purpose.GoingHome) || previous.Equals(Purpose.Sleeping) || previous.Equals(Purpose.None))
                                            {
                                                hbsch[h - 24]++;
                                            }
                                            else
                                            {
                                                nhb[h - 24]++;
                                            }
                                        }
                                        else
                                        {
                                            // For cases where status is "Studying", but there is no "GoingToSchool" status before between now and previous location
                                            if (current.Equals(Purpose.Studying))
                                            {
                                                if (previous.Equals(Purpose.Sleeping) || previous.Equals(Purpose.None))
                                                {
                                                    hbsch[h - 24]++;
                                                }
                                                else
                                                {
                                                    if (!previous.Equals(Purpose.GoingToSchool))
                                                    {
                                                        nhb[h - 24]++;
                                                    }

                                                }
                                            }
                                            else
                                            {
                                                //Going Home
                                                if (current.Equals(Purpose.GoingHome))
                                                {
                                                    if (h == 24 && next_hour == 25 && next.Equals(Purpose.Working) && previous.Equals(Purpose.Working))
                                                    {
                                                        continue;
                                                    }
                                                    if (previous.Equals(Purpose.Working) || (previous.Equals(Purpose.GoingToWork)))
                                                    {
                                                        if ((next.Equals(Purpose.Sleeping) || next.Equals(Purpose.None) || next.Equals(Purpose.GoingToWork)) || (Math.Abs(next_hour - h) > 1))
                                                        {
                                                            hbw[h - 24]++;
                                                        }
                                                        else
                                                        {
                                                            nhb[h - 24]++;
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (previous.Equals(Purpose.Studying) || previous.Equals(Purpose.GoingToSchool))
                                                        {
                                                            if ((next.Equals(Purpose.Sleeping) || next.Equals(Purpose.None)) || (Math.Abs(next_hour - h) > 1))
                                                            {
                                                                hbsch[h - 24]++;
                                                            }
                                                            else
                                                            {
                                                                nhb[h - 24]++;
                                                            }
                                                        }
                                                        else
                                                        {
                                                            if (previous.Equals(Purpose.Sleeping))
                                                            {
                                                                continue;
                                                            }
                                                            if ((next.Equals(Purpose.Sleeping) || next.Equals(Purpose.None)) || (Math.Abs(next_hour - h) > 1))
                                                            {
                                                                hbo[h - 24]++;
                                                            }
                                                            else
                                                            {
                                                                if (!previous.Equals(Purpose.None) && !previous.Equals(Purpose.Sleeping))
                                                                {
                                                                    if ((Math.Abs(next_hour - h) > 2))
                                                                    {
                                                                        hbo[h - 24]++;
                                                                    }
                                                                    else
                                                                    {
                                                                        nhb[h - 24]++;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    if (current.Equals(Purpose.None))
                                                    {
                                                        continue;
                                                    }
                                                    if (previous.Equals(Purpose.Sleeping) || previous.Equals(Purpose.None) || previous.Equals(Purpose.GoingHome))
                                                    {
                                                        if (!current.Equals(Purpose.Sleeping))
                                                        {
                                                            if (!(current.Equals(Purpose.GoingToSchool) || current.Equals(Purpose.GoingToWork) || current.Equals(Purpose.Studying) || current.Equals(Purpose.Working)))
                                                            {
                                                                hbo[h - 24]++;
                                                            }
                                                            else
                                                            {
                                                                nhb[h - 24]++;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        thour = null;
                        tpurp = null;
                    }
                    
                    Dictionary<int, Purpose[]> _outputDataTemp = new Dictionary<int, Purpose[]>();
                    int[] keys = _outputData.Keys.ToArray<int>();
                    foreach (var key in keys)
                    {
                        Purpose[] purp;
                        if (_outputData.TryGetValue(key, out purp))
                        {
                            for (int h = 0; h < 24; h++)
                            {
                                purp[h] = purp[h + 48];
                                purp[h + 24] = 0;
                                purp[h + 48] = 0;
                                _outputData[key] = purp;
                            }
                        }
                    }

                    if (Mod.m_Setting.trip_type)
                    {
                        path = Path.Combine(Mod.outputPath, Mod.tripsoutput);
                        fileName = path +
                            "_" + currentDateTime.DayOfYear + "_" + currentDateTime.Year + ".csv";

                        if (File.Exists(fileName))
                        {
                            File.Delete(fileName);
                        }

                        Mod.log.Info($"Creating file: {fileName}");
                        using (StreamWriter sw = File.AppendText(fileName))
                        {
                            sw.WriteLine($"hour,trip_purpose,trips");
                            for (int h = 0; h < 48; h++)
                            {
                                sw.WriteLine($"{h / 2f},hbw,{hbw[h]}");
                                sw.WriteLine($"{h / 2f},hbsch,{hbsch[h]}");
                                sw.WriteLine($"{h / 2f},hbo,{hbo[h]}");
                                sw.WriteLine($"{h / 2f},nhb,{nhb[h]}");
                            }
                        }
                        // Get the files
                        DirectoryInfo info = new DirectoryInfo(Mod.outputPath);
                        FileInfo[] files = info.GetFiles(Mod.tripsoutput + "*");

                        // Sort by creation-time descending 
                        Array.Sort(files, delegate (FileInfo f1, FileInfo f2)
                        {
                            return f1.CreationTime.CompareTo(f2.CreationTime);
                        });

                        while (files.Length > Mod.m_Setting.numOutputs)
                        {
                            Mod.log.Info($"Deleting: {files[0].FullName}");
                            File.Delete(files[0].FullName);

                            // Get the files
                            info = new DirectoryInfo(path);
                            files = info.GetFiles(Mod.tripsoutput + "*");

                            // Sort by creation-time descending 
                            Array.Sort(files, delegate (FileInfo f1, FileInfo f2)
                            {
                                return f1.CreationTime.CompareTo(f2.CreationTime);
                            });
                        }
                    }
                }
            }
        }
    }
}
