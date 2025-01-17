﻿using HaroohieClub.NitroPacker.Core;
using HaruhiChokuretsuLib.Archive;
using HaruhiChokuretsuLib.Archive.Data;
using HaruhiChokuretsuLib.Archive.Event;
using HaruhiChokuretsuLib.Archive.Graphics;
using HaruhiChokuretsuLib.Util;
using SerialLoops.Lib.Util;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SerialLoops.Lib
{
    public static class Build
    {
        public static bool BuildIterative(Project project, Config config, ILogger log, IProgressTracker tracker)
        {
            bool result = DoBuild(project.IterativeDirectory, project, config, log, tracker);
            CopyToArchivesToIterativeOriginal(Path.Combine(project.IterativeDirectory, "rom", "data"),
                Path.Combine(project.IterativeDirectory, "original", "archives"), log, tracker);
            if (result)
            {
                CleanIterative(project, log, tracker);
            }
            return result;
        }

        public static bool BuildBase(Project project, Config config, ILogger log, IProgressTracker tracker)
        {
            bool result = DoBuild(project.BaseDirectory, project, config, log, tracker);
            CopyToArchivesToIterativeOriginal(Path.Combine(project.BaseDirectory, "rom", "data"),
                Path.Combine(project.IterativeDirectory, "original", "archives"), log, tracker);
            if (result)
            {
                CleanIterative(project, log, tracker);
            }
            return result;
        }

        private static void CleanIterative(Project project, ILogger log, IProgressTracker tracker)
        {
            string[] preservedFiles = Array.Empty<string>();
            string[] cleanableFiles = Directory.GetFiles(Path.Combine(project.IterativeDirectory, "assets"), "*", SearchOption.AllDirectories);
            tracker.Focus("Cleaning Iterative Directory", cleanableFiles.Length);
            foreach (string file in cleanableFiles)
            {
                if (!preservedFiles.Contains(Path.GetFileName(file)))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (IOException exc)
                    {
                        log.LogError($"Failed to clean iterative directory: {exc.Message}\n\n{exc.StackTrace}");
                    }
                }
                tracker.Finished++;
            }
        }

        private static bool DoBuild(string directory, Project project, Config config, ILogger log, IProgressTracker tracker)
        {
            // Export includes
            StringBuilder commandsIncSb = new();
            foreach (ScriptCommand command in EventFile.CommandsAvailable)
            {
                commandsIncSb.AppendLine(command.GetMacro());
            }

            tracker.Focus("Loading Archives (dat.bin)", 3);
            var dat = ArchiveFile<DataFile>.FromFile(Path.Combine(directory, "original", "archives", "dat.bin"), log);
            tracker.Finished++;
            tracker.CurrentlyLoading = "Loading Archives (evt.bin)";
            var evt = ArchiveFile<EventFile>.FromFile(Path.Combine(directory, "original", "archives", "evt.bin"), log);
            tracker.Finished++;
            tracker.CurrentlyLoading = "Loading Archives (grp.bin)";
            var grp = ArchiveFile<GraphicsFile>.FromFile(Path.Combine(directory, "original", "archives", "grp.bin"), log);

            if (dat is null || evt is null || grp is null)
            {
                log.LogError("One or more archives is null.");
                return false;
            }

            tracker.Focus("Writing Includes", 4);
            try
            {
                File.WriteAllText(Path.Combine(directory, "COMMANDS.INC"), commandsIncSb.ToString());
                File.WriteAllText(Path.Combine(directory, "DATBIN.INC"), dat.GetSourceInclude());
                File.WriteAllText(Path.Combine(directory, "EVTBIN.INC"), evt.GetSourceInclude());
                File.WriteAllText(Path.Combine(directory, "GRPBIN.INC"), grp.GetSourceInclude());
            }
            catch (IOException exc)
            {
                log.LogError($"Failed to write include files to disk: {exc.Message}\n\n{exc.StackTrace}");
                return false;
            }
            tracker.Finished+= 4;

            // Replace files
            string[] files = Directory.GetFiles(Path.Combine(directory, "assets"), "*.*", SearchOption.AllDirectories);
            tracker.Focus("Replacing Files", files.Length);
            foreach (string file in files)
            {
                if (int.TryParse(Path.GetFileNameWithoutExtension(file).Split('_')[0], NumberStyles.HexNumber, new CultureInfo("en-US"), out int index) || Path.GetFileName(file).StartsWith("new", StringComparison.OrdinalIgnoreCase))
                {
                    if (index > 0)
                    {
                        if (Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase))
                        {
                            ReplaceSingleGraphicsFile(grp, file, index, log);
                        }
                        else if (file.EndsWith("_pal.csv", StringComparison.OrdinalIgnoreCase))
                        {
                            // ignore palette files as they will be handled by the PNGs above
                        }
                        else if (Path.GetExtension(file).Equals(".s", StringComparison.OrdinalIgnoreCase))
                        {
                            if (string.IsNullOrEmpty(config.DevkitArmPath))
                            {
                                log.LogError("DevkitARM must be supplied in order to build!");
                                return false;
                            }
                            if (file.Contains("events"))
                            {
                                ReplaceSingleSourceFile(evt, file, index, config.DevkitArmPath, directory, log);
                            }
                            else if (file.Contains("data"))
                            {
                                ReplaceSingleSourceFile(dat, file, index, config.DevkitArmPath, directory, log);
                            }
                            else
                            {
                                log.LogWarning($"Source file found at '{file}', outside of data and events directory; skipping...");
                            }
                        }
                        else if (Path.GetExtension(file).Equals(".bna", StringComparison.OrdinalIgnoreCase))
                        {
                            ReplaceSingleAnimationFile(grp, file, index, log);
                        }
                        else
                        {
                            log.LogError($"Unsure what to do with file '{Path.GetFileName(file)}'");
                            return false;
                        }
                    }
                    else
                    {
                        //AddNewFile(archive, filePath, log);
                    }
                }
                tracker.Finished++;
            }

            // Save files to disk
            tracker.Focus("Writing Replaced Archives", 3);
            if (!IO.WriteBinaryFile(Path.Combine(directory, "rom", "data", "dat.bin"), dat.GetBytes(), log))
            {
                return false;
            }
            if (!IO.WriteBinaryFile(Path.Combine(directory, "rom", "data", "evt.bin"), evt.GetBytes(), log))
            {
                return false;
            }
            if (!IO.WriteBinaryFile(Path.Combine(directory, "rom", "data", "grp.bin"), grp.GetBytes(), log))
            {
                return false;
            }
            tracker.Finished+= 3;

            // Save project file to disk
            string ndsProjectFile = Path.Combine(directory, "rom", $"{project.Name}.xml");
            tracker.Focus("Writing NitroPacker Project File", 1);
            try
            {
                File.WriteAllBytes(ndsProjectFile, project.Settings.File.Write());
            } 
            catch (IOException exc)
            {
                log.LogError($"Failed to write NitroPacker NDS project file to disk: {exc.Message}\n\n{exc.StackTrace}");
                return false;
            }
            tracker.Finished++;

            tracker.Focus("Packing ROM", 1);
            try
            {
                NdsProjectFile.Pack(Path.Combine(project.MainDirectory, $"{project.Name}.nds"), ndsProjectFile);
            }
            catch (Exception exc)
            {
                log.LogError($"NitroPacker failed to pack ROM with exception '{exc.Message}'\n\n{exc.StackTrace}");
                return false;
            }
            tracker.Finished++;

            return true;
        }

        private static void CopyToArchivesToIterativeOriginal(string newDataDir, string iterativeOriginalDir, ILogger log, IProgressTracker tracker)
        {
            tracker.Focus("Copying Archives to Iterative Originals", 3);
            try
            {
                File.Copy(Path.Combine(newDataDir, "dat.bin"), Path.Combine(iterativeOriginalDir, "dat.bin"), overwrite: true);
                File.Copy(Path.Combine(newDataDir, "evt.bin"), Path.Combine(iterativeOriginalDir, "evt.bin"), overwrite: true);
                File.Copy(Path.Combine(newDataDir, "grp.bin"), Path.Combine(iterativeOriginalDir, "grp.bin"), overwrite: true);
            }
            catch (IOException exc)
            {
                log.LogError($"Failed to copy newly built archives to the iterative originals directory.\n{exc.Message}\n\n{exc.StackTrace}");
            }
            tracker.Finished+= 3;
        }

        private static void ReplaceSingleGraphicsFile(ArchiveFile<GraphicsFile> grp, string filePath, int index, ILogger log)
        {
            try
            {
                GraphicsFile grpFile = grp.Files.FirstOrDefault(f => f.Index == index);

                if (index == 0xE50)
                {
                    grpFile.InitializeFontFile();
                }

                string paletteFile = Path.Combine(Path.GetDirectoryName(filePath), $"{Path.GetFileNameWithoutExtension(filePath)}_pal.csv");
                if (File.Exists(paletteFile))
                {
                    grpFile.SetPalette(File.ReadAllText(paletteFile).Split(',').Select(c => SKColor.Parse(c)).ToList());
                }

                grpFile.SetImage(filePath);

                grp.Files[grp.Files.IndexOf(grpFile)] = grpFile;
            }
            catch (Exception ex)
            {
                log.LogException($"Failed replacing graphics file {index} with file '{filePath}'", ex);
            }
        }

        private static bool ReplaceSingleSourceFile(ArchiveFile<EventFile> archive, string filePath, int index, string devkitArm, string workingDirectory, ILogger log)
        {
            try
            {
                (string objFile, string binFile) = CompileSourceFile(filePath, devkitArm, workingDirectory, log);
                if (!File.Exists(binFile))
                {
                    log.LogError($"Compiled file {binFile} does not exist!");
                    return false;
                }
                ReplaceSingleFile(archive, binFile, index, log);
                File.Delete(objFile);
                File.Delete(binFile);
                return true;
            }
            catch (Exception ex)
            {
                log.LogException($"Failed replacing source file {index} in evt.bin with file '{filePath}'", ex);
                return false;
            }
        }
        private static bool ReplaceSingleSourceFile(ArchiveFile<DataFile> archive, string filePath, int index, string devkitArm, string workingDirectory, ILogger log)
        {
            try
            {
                (string objFile, string binFile) = CompileSourceFile(filePath, devkitArm, workingDirectory, log);
                if (!File.Exists(binFile))
                {
                    log.LogError($"Compiled file {binFile} does not exist!");
                    return false;
                }
                ReplaceSingleFile(archive, binFile, index, log);
                File.Delete(objFile);
                File.Delete(binFile);
                return true;
            }
            catch (Exception ex)
            {
                log.LogException($"Failed replacing source file {index} in dat.bin with file '{filePath}'", ex);
                return false;
            }
        }

        private static (string, string) CompileSourceFile(string filePath, string devkitArm, string workingDirectory, ILogger log)
        {
            string exeExtension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;

            string objFile = $"{Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath))}.o";
            string binFile = $"{Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath))}.bin";
            ProcessStartInfo gccStartInfo = new(Path.Combine(devkitArm, "bin", $"arm-none-eabi-gcc{exeExtension}"), $"-c -nostdlib -static \"{filePath}\" -o \"{objFile}")
            {
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (!File.Exists(gccStartInfo.FileName))
            {
                log.LogError($"gcc not found at '{gccStartInfo.FileName}'");
                return (string.Empty, string.Empty);
            }
            log.Log($"Compiling '{filePath}' to '{objFile}' with '{gccStartInfo.FileName}'...");
            Process gcc = new() { StartInfo = gccStartInfo };
            gcc.OutputDataReceived += (object sender, DataReceivedEventArgs e) => log.Log(e.Data);
            gcc.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => log.LogWarning(e.Data);
            gcc.Start();
            gcc.WaitForExit();
            Task.Delay(50); // ensures process is actually complete
            if (gcc.ExitCode != 0)
            {
                log.LogError($"gcc exited with code {gcc.ExitCode}");
                return (string.Empty, string.Empty);
            }

            ProcessStartInfo objcopyStartInfo = new(Path.Combine(devkitArm, "bin", $"arm-none-eabi-objcopy{exeExtension}"), $"-O binary \"{objFile}\" \"{binFile}")
            {
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (!File.Exists(objcopyStartInfo.FileName))
            {
                log.LogError($"objcopy not found at '{objcopyStartInfo.FileName}'");
                return (string.Empty, string.Empty);
            }
            log.Log($"Objcopying '{objFile}' to '{binFile}' with '{objcopyStartInfo.FileName}'...");
            Process objcopy = new() { StartInfo = objcopyStartInfo };
            objcopy.OutputDataReceived += (object sender, DataReceivedEventArgs e) => log.Log(e.Data);
            objcopy.ErrorDataReceived += (object sender, DataReceivedEventArgs e) => log.LogWarning(e.Data);
            objcopy.Start();
            objcopy.WaitForExit();
            Task.Delay(50); // ensures process is actually complete
            if (objcopy.ExitCode != 0)
            {
                log.LogError($"objcopy exited with code {objcopy.ExitCode}");
                return (string.Empty, string.Empty);
            }

            return (objFile, binFile);
        }

        private static void ReplaceSingleFile(ArchiveFile<EventFile> archive, string filePath, int index, ILogger log)
        {
            try
            {
                EventFile file = archive.Files.FirstOrDefault(f => f.Index == index);
                file.Data = File.ReadAllBytes(filePath).ToList();
                file.Edited = true;
                archive.Files[archive.Files.IndexOf(file)] = file;
            }
            catch (Exception ex)
            {
                log.LogException($"Failed replacing source file {index} in evt.bin with file '{filePath}'", ex);
            }
        }
        private static void ReplaceSingleFile(ArchiveFile<DataFile> archive, string filePath, int index, ILogger log)
        {
            try
            {
                DataFile file = archive.Files.FirstOrDefault(f => f.Index == index);
                file.Data = File.ReadAllBytes(filePath).ToList();
                file.Edited = true;
                archive.Files[archive.Files.IndexOf(file)] = file;
            }
            catch (Exception ex)
            {
                log.LogException($"Failed replacing source file {index} in dat.bin with file '{filePath}'", ex);
            }
        }
        private static void ReplaceSingleAnimationFile(ArchiveFile<GraphicsFile> archive, string filePath, int index, ILogger log)
        {
            try
            {
                GraphicsFile file = archive.Files.FirstOrDefault(f => f.Index == index);
                GraphicsFile newFile = new()
                {
                    Name = file.Name,
                    Index = file.Index,
                };
                newFile.Initialize(File.ReadAllBytes(filePath), file.Offset, log);
                newFile.Edited = true;
                archive.Files[archive.Files.IndexOf(file)] = newFile;
            }
            catch (Exception ex)
            {
                log.LogException($"Failed replacing file {index} in grp.bin with file '{filePath}'", ex);
            }
        }
    }
}
