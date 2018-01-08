﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;


namespace OverParse
{
    public class Log
    {
        private int startTimestamp = 0;
        public int newTimestamp = 0;
        private string encounterData;
        private List<int> instances = new List<int>();
        public List<Combatant> combatants = new List<Combatant>();
        public List<Combatant> backupCombatants = new List<Combatant>();
        private const int pluginVersion = 5;
        public bool valid;
        public bool notEmpty;
        public bool running;
        public DirectoryInfo logDirectory;
        public string filename;
        private StreamReader logReader;


        public Log(string attemptDirectory)
        {
            bool nagMe = false;
            valid = false;
            notEmpty = false;
            running = false;

            if (Properties.Settings.Default.BanWarning)
            {
                MessageBoxResult panicResult = MessageBox.Show("OverParseは、PSO2の規約を破る外部ツールです。\nSEGAは公式に解析ツールを使用した事が確認されたアカウントを停止する可能性があると発表しました。\n\nアカウントの安全性を保つ場合にはOverParseを使用しないで下さい。\nこのツールの使用は自己責任です。\n\nセットアップを続けますか？", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (panicResult == MessageBoxResult.No) { Environment.Exit(-1); }
                Properties.Settings.Default.BanWarning = false;
            }

            while (!File.Exists($"{attemptDirectory}\\pso2.exe"))
            {
                if (nagMe)
                {
                    MessageBox.Show("これは有効なpso2_binディレクトリではありません。\npso2.exeが見つかりません。", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                } else {
                    MessageBox.Show("pso2_binディレクトリを選択して下さい。\nOverParseはダメージログを読み込みます。\n\nTweakerを設定しているときにフォルダを選択した場合は、そのフォルダを選択します。\nその他の場合にはPSO2のインストールフォルダを選択します。", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    nagMe = true;
                }

                //WINAPI FILE DIALOGS DON'T SHOW UP FOR PEOPLE SOMETIMES AND I HAVE NO IDEA WHY, *** S I C K  M E M E ***
                //VistaFolderBrowserDialog oDialog = new VistaFolderBrowserDialog();
                //oDialog.Description = "Select your pso2_bin folder...";
                //oDialog.UseDescriptionForTitle = true;

                System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog { Description = "pso2_binフォルダを選択して下さい。\n大抵の場合はPSO2をインストールしたフォルダにあります。" };
                System.Windows.Forms.DialogResult picked = dialog.ShowDialog();
                if (picked == System.Windows.Forms.DialogResult.OK)
                {
                    attemptDirectory = dialog.SelectedPath;
                    Properties.Settings.Default.Path = attemptDirectory;
                } else {
                    MessageBox.Show("必要なインストールが設定されませんでした。\nアプリケーションを終了します。", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    Environment.Exit(-1);
                    break;
                }
            }

            if (!File.Exists($"{attemptDirectory}\\pso2.exe")) { return; }
            valid = true;
            logDirectory = new DirectoryInfo($"{attemptDirectory}\\damagelogs");

            //- - - - 

            bool pluginsExist = File.Exists(attemptDirectory + "\\pso2h.dll") && File.Exists(attemptDirectory + "\\ddraw.dll") && File.Exists(attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll");
            if (!pluginsExist) { Properties.Settings.Default.InstalledPluginVersion = -1; }

            if (Properties.Settings.Default.InstalledPluginVersion < pluginVersion)
            {
                MessageBoxResult selfdestructResult;

                if (pluginsExist)
                {
                    selfdestructResult = MessageBox.Show("このOverParseのバージョンには、dllプラグインの新しいバージョンが含まれています。\n今すぐ更新しますか？", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                } else {
                    selfdestructResult = MessageBox.Show("dllプラグインをインストールしますか？", "OverParse Setup", MessageBoxButton.YesNo, MessageBoxImage.Question);
                }

                if (selfdestructResult == MessageBoxResult.No && !pluginsExist)
                {
                    MessageBox.Show("OverParseにはdllプラグインが必要です。\nアプリケーションを終了します。", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                    Environment.Exit(-1);
                    return;
                } else if (selfdestructResult == MessageBoxResult.Yes) {
                    bool success = UpdatePlugin(attemptDirectory);
                    if (!pluginsExist && !success)
                        Environment.Exit(-1);
                }
            }

            Properties.Settings.Default.FirstRun = false;

            if (!logDirectory.Exists)
                return;
            if (logDirectory.GetFiles().Count() == 0)
                return;

            notEmpty = true;

            FileInfo log = logDirectory.GetFiles().Where(f => Regex.IsMatch(f.Name, @"\d+\.")).OrderByDescending(f => f.Name).First();
            //Console.WriteLine($"Reading from {log.DirectoryName}\\{log.Name}");
            filename = log.Name;
            FileStream fileStream = File.Open(log.DirectoryName + "\\" + log.Name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            fileStream.Seek(0, SeekOrigin.Begin);
            logReader = new StreamReader(fileStream);

            string existingLines = logReader.ReadToEnd(); // gotta get the dummy line for current player name
            string[] result = existingLines.Split('\n');
            foreach (string s in result)
            {
                if (s == "")
                    continue;
                string[] parts = s.Split(',');
                if (parts[0] == "0" && parts[3] == "YOU") { Hacks.currentPlayerID = parts[2]; }
            }
        }

        public bool UpdatePlugin(string attemptDirectory)
        {
            try
            {
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\pso2h.dll", attemptDirectory + "\\pso2h.dll", true);
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\ddraw.dll", attemptDirectory + "\\ddraw.dll", true);
                Directory.CreateDirectory(attemptDirectory + "\\plugins");
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\PSO2DamageDump.dll", attemptDirectory + "\\plugins" + "\\PSO2DamageDump.dll", true);
                File.Copy(Directory.GetCurrentDirectory() + "\\resources\\PSO2DamageDump.cfg", attemptDirectory + "\\plugins" + "\\PSO2DamageDump.cfg", true);
                Properties.Settings.Default.InstalledPluginVersion = pluginVersion;
                MessageBox.Show("セットアップ完了！\ndllプラグインがpso2_binフォルダにコピーされています。\nPSO2が既に実行されている場合は再起動する必要があります。", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Information);
                return true;
            } catch {
                MessageBox.Show("インストールで何か問題が発生しました。\nこれは通常、ファイルが既に使用中である事を意味します。\nPSO2を閉じた状態でもう一度やり直してみて下さい。\nもしくは、管理者としてOverParseを実行する必要があります。", "OverParse Setup", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        public void WriteClipboard()
        {
            string log = "";
            foreach (Combatant c in combatants)
            {
                if (c.IsAlly)
                {
                    string shortname = c.Name;
                    if (c.Name.Length > 6) { shortname = c.Name.Substring(0, 6); }

                    log += $"{shortname} {(c.Damage).ToString("N0")} | ";
                }
            }

            if (log == "") { return; }
            log = log.Substring(0, log.Length - 2);

            try
            {
                Clipboard.SetText(log);
            } catch {
                MessageBox.Show("クリップボードにデータを保存できませんでした。");
            }

        }

        public string WriteLog()
        {

            /*
            foreach (Combatant c in combatants) // Debug for ID mapping
            {
                if (c.IsAlly)
                {
                    foreach (Attack a in c.Attacks)
                    {
                        if (!MainWindow.skillDict.ContainsKey(a.ID))
                        {
                            TimeSpan t = TimeSpan.FromSeconds(a.Timestamp);
                            //Console.WriteLine($"{t.ToString(@"dd\.hh\:mm\:ss")} unmapped: {a.ID} ({a.Damage} dmg from {c.Name})");
                        }
                    }
                }
            } */

            if (combatants.Count != 0)
            {
                int elapsed = newTimestamp - startTimestamp;
                TimeSpan timespan = TimeSpan.FromSeconds(elapsed);
                string timer = timespan.ToString(@"mm\:ss");
                string log = DateTime.Now.ToString("F") + " | " + timer + " | "  + Environment.NewLine + Environment.NewLine;

                foreach (Combatant c in combatants)
                {
                    try
                    {
                        if (c.IsAlly || c.IsZanverse || c.IsFinish)
                            log += $"{c.Name} | {c.RatioPercent}% | {c.ReadDamage.ToString("N0")} dmg | {c.Damaged} dmgd | {c.DPS} DPS | JA : {c.WJAPercent}% | Critical : {c.WCRIPercent}% | Max:{c.MaxHitdmg} ({c.MaxHit})" + Environment.NewLine;
                    }
                    catch{/* 今の所何もしないっぽい */}
                }

                log += Environment.NewLine + Environment.NewLine;

                foreach (Combatant c in combatants)
                {
                    if (c.IsAlly || c.IsZanverse || c.IsFinish)
                    {
                        string header = $"[ {c.Name} - {c.RatioPercent}% - {c.ReadDamage.ToString("N0")} dmg ]";
                        log += header + Environment.NewLine + Environment.NewLine;

                        List<string> attackNames = new List<string>();
                        List<string> finishNames = new List<string>();
                        List<Tuple<string, List<Int64>>> attackData = new List<Tuple<string, List<Int64>>>();

                        if (c.IsZanverse && Properties.Settings.Default.SeparateZanverse)
                        {
                            foreach (Combatant c2 in backupCombatants)
                            {
                                if (c2.GetZanverseDamage > 0) { attackNames.Add(c2.ID); }
                            }

                            foreach (string s in attackNames)
                            {
                                Combatant targetCombatant = backupCombatants.First(x => x.ID == s);
                                List<Int64> matchingAttacks = targetCombatant.Attacks.Where(a => a.ID == "2106601422").Select(a => a.Damage).ToList();
                                //List<int> jaPercents = c.Attacks.Where(a => a.ID == s).Select(a => a.JA).ToList();
                                //List<int> criPercents = c.Attacks.Where(a => a.ID == s).Select(a => a.Cri).ToList();
                                attackData.Add(new Tuple<string, List<Int64>>(targetCombatant.Name, matchingAttacks));
                            }
                        }

                        else if (c.IsFinish && Properties.Settings.Default.SeparateFinish)
                        {
                            foreach (Combatant c3 in backupCombatants)
                            {
                                if (c3.GetFinishDamage > 0) { finishNames.Add(c3.ID); }
                            }

                            foreach (string htf in finishNames)
                            {
                                Combatant tCombatant = backupCombatants.First(x => x.ID == htf);
                                List<Int64> fmatchingAttacks = tCombatant.Attacks.Where(a => Combatant.FinishAttackIDs.Contains(a.ID)).Select(a => a.Damage).ToList();
                                attackData.Add(new Tuple<string, List<Int64>>(tCombatant.Name, fmatchingAttacks));
                            }
                        } else {
                            foreach (Attack a in c.Attacks)
                            {
                                if ((a.ID == "2106601422" && Properties.Settings.Default.SeparateZanverse) || (Combatant.FinishAttackIDs.Contains(a.ID) && Properties.Settings.Default.SeparateFinish)) { continue; } //ザンバースの場合に何もしない
                                if (MainWindow.skillDict.ContainsKey(a.ID)) { a.ID = MainWindow.skillDict[a.ID]; } // these are getting disposed anyway, no 1 cur
                                if (!attackNames.Contains(a.ID.ToString())) { attackNames.Add(a.ID); }
                            }

                            foreach (string s in attackNames)
                            {
                                //マッチングアタックからダメージを選択するだけ
                                List<Int64> matchingAttacks = c.Attacks.Where(a => a.ID == s).Select(a => a.Damage).ToList();
                                //List<int> jaPercents = c.Attacks.Where(a => a.ID == s).Select(a => a.JA).ToList();
                                //List<int> criPercents = c.Attacks.Where(a => a.ID == s).Select(a => a.Cri).ToList();
                                attackData.Add(new Tuple<string, List<Int64>>(s, matchingAttacks));
                            }
                        }
                        
                        attackData = attackData.OrderByDescending(x => x.Item2.Sum()).ToList();

                        foreach (var i in attackData)
                        {
                            double percent = i.Item2.Sum() * 100d / c.ReadDamage;
                            string spacer = (percent >= 9) ? "" : " ";

                            string paddedPercent = percent.ToString("00.00").Substring(0, 5);
                            string hits = i.Item2.Count().ToString("N0") ?? "0";
                            string sum = i.Item2.Sum().ToString("N0");
                            string min = i.Item2.Min().ToString("N0");
                            string max = i.Item2.Max().ToString("N0");
                            string avg = i.Item2.Average().ToString("N0");
                            //string ja = (i.Item3.Average() * 100).ToString("N2") ?? "null";
                            //string cri = (i.Item4.Average() * 100).ToString("N2") ?? "null" ;
                            log += $"{paddedPercent}%	| {i.Item1} - {sum} dmg";
                            //log += $" - JA : {ja}% - Critical : {cri}%";
                            log += Environment.NewLine;
                            log += $"	|   {hits} hits - {min} min, {avg} avg, {max} max" + Environment.NewLine;
                        }

                        log += Environment.NewLine;
                    }
                }


                log += "Instance IDs: " + String.Join(", ", instances.ToArray());

                DateTime thisDate = DateTime.Now;
                string directory = string.Format("{0:yyyy-MM-dd}", DateTime.Now);
                Directory.CreateDirectory($"Logs/{directory}");
                string datetime = string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now);
                string filename = $"Logs/{directory}/OverParse - {datetime}.txt";
                File.WriteAllText(filename, log);

                return filename;
            }
            return null;
        }

        public string LogStatus()
        {
            if (!valid) { return "USER SHOULD PROBABLY NEVER SEE THIS"; }
            if (!notEmpty) { return "Directory No logs: Enable plugin and check pso2_bin!"; }
            if (!running) { return $"Waiting..."; }
            return encounterData;
        }

        public void UpdateLog(object sender, EventArgs e)
        {
            if (!valid || !notEmpty)  { return; }
            string newLines = logReader.ReadToEnd();

            if (newLines != "")
            {
                string[] result = newLines.Split('\n');
                foreach (string str in result)
                {
                    if (str != "")
                    {
                        string[] parts = str.Split(',');
                        int lineTimestamp = int.Parse(parts[0]);
                        int instanceID = int.Parse(parts[1]);
                        string sourceID = parts[2];
                        string sourceName = parts[3];
                        string targetID = parts[4];
                        string targetName = parts[5];
                        string attackID = parts[6]; //WriteLog部分にてID<->Nameの相互変換がある為int化が無理
                        Int64 hitDamage = Int64.Parse(parts[7]);
                        int justAttack = int.Parse(parts[8]);
                        int critical = int.Parse(parts[9]);
                        //string isMultiHit = parts[10];
                        //string isMisc = parts[11];
                        //string isMisc2 = parts[12];
                        int index = -1;

                        if (lineTimestamp == 0 && parts[3] == "YOU")
                        {
                            Hacks.currentPlayerID = parts[2];
                            continue;
                        }

                        if (sourceID != Hacks.currentPlayerID && Properties.Settings.Default.Onlyme) { continue; }
                        if (!instances.Contains(instanceID)) { instances.Add(instanceID); }
                        if (hitDamage < 1) { continue; }
                        if (sourceID == "0" || attackID == "0") { continue; }

                        //処理スタート
                        newTimestamp = lineTimestamp;
                        if (startTimestamp == 0)
                        {
                            //Console.WriteLine($"FIRST ATTACK RECORDED: {hitDamage} dmg from {sourceID} ({sourceName}) with {attackID}, to {targetID} ({targetName})");
                            startTimestamp = newTimestamp;
                        }


                        if (10000000 < int.Parse(sourceID))
                        {
                            foreach (Combatant x in combatants)
                            {
                                if (x.ID == sourceID && x.isTemporary == "no") { index = combatants.IndexOf(x); }
                            }

                            if (index == -1)
                            {
                                combatants.Add(new Combatant(sourceID, sourceName));
                                index = combatants.Count - 1;
                            }

                            Combatant source = combatants[index];

                            source.Attacks.Add(new Attack(attackID, hitDamage, newTimestamp - startTimestamp, justAttack, critical, 0));
                            running = true;
                        } else {
                            foreach (Combatant x in combatants)
                            {
                                if (x.ID == targetID && x.isTemporary == "no") { index = combatants.IndexOf(x); }
                            }

                            if (index == -1)
                            {
                                combatants.Add(new Combatant(targetID, targetName));
                                index = combatants.Count - 1;
                            }

                            Combatant source = combatants[index];

                            source.Attacks.Add(new Attack("0", 0, newTimestamp - startTimestamp, 0, 0, hitDamage));
                            running = true;
                        }
                    }
                }

                combatants.Sort((x, y) => y.ReadDamage.CompareTo(x.ReadDamage));

                if (startTimestamp != 0) { encounterData = "0:00:00 - ∞ DPS"; }

                if (startTimestamp != 0 && newTimestamp != startTimestamp)
                {
                    foreach (Combatant x in combatants)
                    {
                        if (x.IsAlly || x.IsZanverse) { x.ActiveTime = (newTimestamp - startTimestamp); }
                    }
                }
            }
        }

    }
}
