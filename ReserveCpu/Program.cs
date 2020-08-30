using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;

namespace ReserveCpu
{
	class Program
	{
		class ProcessConfig
		{
			public string ProcName = "";
			public ProcessPriorityClass Priority = ProcessPriorityClass.Normal;
		}

		static List<List<ProcessConfig>> reservelist = new List<List<ProcessConfig>>();

		static List<string> ignorelist = new List<string>();

		static bool rtwarn = false;
		static bool highwarn = false;
		static bool anwarn = false;
		static bool plwarn = false;

		static void LoadConfig()
		{
			XmlDocument configfile = new XmlDocument();
			configfile.Load("config.xml");

			foreach (XmlNode config in configfile.ChildNodes)
			{
				if (config.Name == "config")
				{
					foreach (XmlNode node in config.ChildNodes)
					{
						if (node.Name == "core")
						{
							List<ProcessConfig> procs = new List<ProcessConfig>();

							foreach (XmlNode procnode in node.ChildNodes)
							{
								if (procnode.Name == "proc")
								{
									ProcessConfig proc = new ProcessConfig();

									proc.ProcName = procnode.Attributes["name"]?.Value ?? "";

									switch (procnode.Attributes["prio"]?.Value ?? "")
									{
										case "idle":
											proc.Priority = ProcessPriorityClass.Idle;
											break;

										case "belownormal":
											proc.Priority = ProcessPriorityClass.BelowNormal;
											break;

										case "normal":
											proc.Priority = ProcessPriorityClass.Normal;
											break;

										case "abovenormal":
											proc.Priority = ProcessPriorityClass.AboveNormal;
											break;

										case "high":
											proc.Priority = ProcessPriorityClass.High;
											break;

										case "realtime":
											proc.Priority = ProcessPriorityClass.RealTime;
											break;

										default:
											proc.Priority = ProcessPriorityClass.Normal;
											break;
									}

									procs.Add(proc);
								}
							}

							reservelist.Add(procs);
						}

						if (node.Name == "ignore")
						{
							foreach (XmlNode ignore in node.ChildNodes)
							{
								ignorelist.Add(ignore.Attributes["name"]?.Value ?? "");
							}
						}
					}
				}
			}
		}

		static bool IsOnTheList(string name, List<ProcessConfig> procs)
		{
			foreach (ProcessConfig proc in procs)
			{
				if (name == proc.ProcName)
				{
					return true;
				}
			}

			return false;
		}

		static bool IsOnIgnoreList(string name)
		{
			foreach (string s in ignorelist)
			{
				if (name == s)
				{
					return true;
				}
			}

			return false;
		}

		static ProcessConfig GetConfig(string name, List<ProcessConfig> procs)
		{
			foreach (ProcessConfig proc in procs)
			{
				if (name == proc.ProcName)
				{
					return proc;
				}
			}

			return new ProcessConfig();
		}

		static void Main(string[] args)
		{
			try
			{
				LoadConfig();
			}
			catch (Exception ex)
			{
				Console.WriteLine("Error loading config file: " + ex.Message);
				return;
			}

			if (reservelist.Count > Environment.ProcessorCount - 1)
			{
				Console.WriteLine("Config error - trying to reserve too many CPU cores.");
			}
			else if (reservelist.Count < 1)
			{
				Console.WriteLine("No CPU cores to reserve, nothing to do.");
			}
			else
			{
				Console.WriteLine("Reserving " + reservelist.Count.ToString() + " CPU cores.");

				for (; !(Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Q);)
				{
					//scan list of processes, check them against the list, and set affinity appropriately
					//keep looping until manually terminated

					Process[] proclist;

					try
					{
						proclist = Process.GetProcesses();
					}
					catch (Exception ex)
					{
						proclist = new Process[0];

						if (!plwarn)
						{
							Console.WriteLine("Error - couldn't fetch system process list: " + ex.Message);
							plwarn = true;
						}
					}

					for (int i = 0; i < reservelist.Count; i++)
					{
						foreach (Process p in proclist)
						{
							if (IsOnTheList(p.ProcessName, reservelist[i]))
							{
								//process matched to list, make it use only this CPU core
								ProcessConfig cfg = GetConfig(p.ProcessName, reservelist[i]);
								p.ProcessorAffinity = (IntPtr)(1 << ((Environment.ProcessorCount - i) - 1));
								p.PriorityClass = cfg.Priority;
							}
							else
							{
								if (!IsOnIgnoreList(p.ProcessName))
								{
									int currentaffinity;

									//process not on list, only allow it to use the other CPU cores
									try
									{
										p.ProcessorAffinity = (IntPtr)((int)(p.ProcessorAffinity) & (~(1 << ((Environment.ProcessorCount - i) - 1))));
									}
									catch (Exception ex)
									{
										Console.WriteLine("Failed to set affinity for process \"" + p.ProcessName + "\": " + ex.Message);
									}
								}
							}
						}
					}

					//allow program to go idle when not scanning
					System.Threading.Thread.Sleep(1000);
				}
			}
		}
	}
}