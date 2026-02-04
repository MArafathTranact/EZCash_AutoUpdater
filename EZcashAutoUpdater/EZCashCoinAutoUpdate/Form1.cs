using Microsoft.Win32;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Xml.Serialization;

namespace EZCashCoinAutoUpdate
{
    public enum InstallationStatus
    {
        Initialized,
        ServiceCheck,
        DownLoading,
        DownLoadCompleted,
        DownLoadFailed,
        CheckDownLoadSizeSH256,
        CheckDownLoadSizeSH256Failed,
        ServiceStopping,
        ServiceStopped,
        ServiceFilesCopying,
        ServiceFilesCopyingCompleted,
        ServiceInstalling,
        ServiceUnInstalling,
        ServiceUnInstalled,
        ServiceInstalled,
        ServiceCreating,
        ServiceCreated,
        ServiceStarting,
        ServiceStarted,
        ServiceReInstalling,
        ServiceReInstalled,
        ServiceUpdateCompleted

    }
    public partial class AutoUpdater : Form
    {

        #region Properties

        private readonly string ServiceName = "EZCoin Dispenser";
        private string appName = "EZCoinDispenserInstaller";

        private string TempPath = string.Empty;
        private List<AutoUpdate> localAutoUpdateConfig = [];
        private List<AutoUpdate> azureAutoUpdateConfig = [];
        private AutoUpdate azureAutoUpdate = new();
        private AutoUpdate localAutoUpdate = new();
        private UpdateEZCashServiceConfiguration configuration = new();
        private string localServiceVersion = string.Empty;
        private string azureServiceVersion = string.Empty;
        private InstallationStatus status = InstallationStatus.Initialized;

        #endregion Properties


        public AutoUpdater()
        {
            InitializeComponent();
        }

        private void btnSaveConfiguration_Click(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(txtServiceDownloadURL.Text) || string.IsNullOrEmpty(txtServiceInstallPath.Text))
                {
                    MessageBox.Show("Provide Service download URL and Install path to continue...");
                    return;
                }


                var config = new UpdateEZCashServiceConfiguration()
                {
                    ServiceInstallPath = txtServiceInstallPath.Text,
                    ServiceDownLoadURL = txtServiceDownloadURL.Text

                };

                var executablePath = Path.GetDirectoryName(Application.ExecutablePath);

                var filePath = Path.Combine(executablePath, "UpdateEZCashCoinServiceConfig.config");

                XmlSerializer serializer = new XmlSerializer(typeof(UpdateEZCashServiceConfiguration));
                using (TextWriter writer = new StreamWriter(filePath))
                {
                    serializer.Serialize(writer, config);
                }
                LogEvents($"EZcash auto update configuration saved. Path = '{filePath}'");
                MessageBox.Show("Settings saved. Relaunch it check for service update");
                Application.Exit();
            }
            catch (Exception ex)
            {
                LogExceptions(" btnSaveConfiguration_Click ", ex);
                MessageBox.Show($"Error Occured while saving. {ex.Message}");
            }

        }

        private async Task<bool> CopyEZCashserviceFiles(string sourcePath, string destinationPath)
        {
            try
            {
                LogEvents($"Moving EZcash files from {sourcePath} to {destinationPath} in case error occured and for recovery process.");
                if (string.IsNullOrEmpty(sourcePath) && !Directory.Exists(sourcePath))
                    return false;
                DirectoryInfo directoryInfo = new DirectoryInfo(sourcePath);

                Directory.CreateDirectory(destinationPath);

                // Move files only first
                foreach (FileInfo file in directoryInfo.GetFiles())
                {
                    var fileName = Path.Combine(destinationPath, file.Name);
                    file.CopyTo(fileName, true);
                    DisplayProgress($"Copying {file.Name}");
                    await Task.Delay(300);
                    LogEvents($"Copying {file.Name} to {destinationPath}");
                }


                //Move Load folder only, exclude everything else.

                var folderInfo = directoryInfo.GetDirectories();
                foreach (var folder in folderInfo)
                {
                    if (folder.Exists && (folder.Name == "Loads" || folder.Name == "Archievedlogs"))
                    {
                        await CopyEZCashserviceFiles(folder.FullName, Path.Combine(destinationPath, folder.Name));
                    }

                }

                return true;
            }
            catch (Exception ex)
            {
                LogExceptions(" CopyEZCashserviceFiles ", ex);
                return false;
            }
        }

        private async Task InitiateAutoUpdateProcess()
        {
            status = InstallationStatus.Initialized;
            LogEvents($"State : {status}");
            LogEvents("Initiating Auto Update process.");
            Observable.FromAsync(() => DownLoadServiceMSI()).Subscribe(async response =>
            {


                if (response.Status)
                {
                    try
                    {
                        await Task.Delay(3000);
                        var msiPath = Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi"); ;

                        if (!File.Exists(msiPath))
                            throw new FileNotFoundException("MSI not found", msiPath);

                        // Extra safety: wait until file is no longer locked
                        using (var stream = File.Open(msiPath, FileMode.Open, FileAccess.Read, FileShare.None))
                        {
                            // If this succeeds, file is ready
                        }

                        LogEvents($"Stopping the current service..");
                        DisplayProgress("Stopping the current service...");

                        status = InstallationStatus.ServiceStopping;
                        LogEvents($"State : {status}");
                        var stopResult = await StopService();

                        if (stopResult)
                        {
                            status = InstallationStatus.ServiceStopped;
                            LogEvents($"State : {status}");
                            var copyResult = await CopyEZCashserviceFiles(configuration.ServiceInstallPath, TempPath);
                        }

                        if (stopResult)
                        {
                            //CenterLabel(lblStatus);
                            DisplayProgress("Uninstalling the current service...");
                            status = InstallationStatus.ServiceUnInstalling;
                            LogEvents($"State : {status}");
                            var uninstallResult = await UnistallService();

                            await Task.Delay(5000);

                            if (uninstallResult)
                            {
                                status = InstallationStatus.ServiceUnInstalled;
                                LogEvents($"State : {status}");
                                DisplayProgress("Successfully Uninstalled the current service...");
                                //CenterLabel(lblStatus);
                                DisplayProgress("Installing the new service");
                                status = InstallationStatus.ServiceInstalling;
                                LogEvents($"State : {status}");
                                var installResult = await InstallNewEzCashService();

                                if (installResult)
                                {
                                    status = InstallationStatus.ServiceInstalled;
                                    LogEvents($"State : {status}");
                                    DisplayProgress("Installed the new service.");
                                    await Task.Delay(2000);
                                    var configCopyResult = CopyConfigToServiceFolder();
                                    if (configCopyResult)
                                    {
                                        status = InstallationStatus.ServiceStarting;
                                        LogEvents($"State : {status}");
                                        DisplayProgress("Starting the new service ...");
                                        LogEvents($"Starting the new service ...");
                                        await Task.Delay(3000);
                                        var startResult = await StartService();

                                        if (startResult)
                                        {
                                            LogEvents($"Service started succesfully.");
                                            status = InstallationStatus.ServiceStarted;
                                            LogEvents($"State : {status}");
                                            await CompleteUpdateProcess();

                                        }
                                        else
                                        {
                                            DisplayProgress("Something went wrong in starting new service.\nManual start needed");
                                            LogEvents($"Something went wrong in starting new service.Manual start needed");
                                        }
                                    }
                                    else
                                    {
                                        DisplayProgress("Error in copying EZCash config file.\nManual interuption needed.");
                                        LogEvents($"\"Error in copying EZCash config file.Manual interuption needed.");
                                    }
                                }
                                else
                                {
                                    DisplayProgress("Error in installing the new service.\nRe-installing the existing service");
                                    LogEvents($"Error in installing the new service.Re-installing the existing service");
                                    await Task.Delay(3000);
                                    status = InstallationStatus.ServiceReInstalling;
                                    LogEvents($"State : {status}");
                                    var reinstall = await ReinstalServiceOnErroredProcess();
                                }


                            }
                            else
                            {
                                DisplayProgress("Error in uninstalling the existing service.");
                                await Task.Delay(1500);
                                DisplayProgress("Restarting the existing service.");
                                LogEvents($"Error in uninstalling the existing service.Auto update interupted.Restarting the existing service.");
                                await Task.Delay(2000);
                                status = InstallationStatus.ServiceReInstalling;
                                LogEvents($"State : {status}");
                                var reinstall = await ReinstalServiceOnErroredProcess();
                            }
                        }
                        else
                        {
                            DisplayProgress("Error occured in stopping the service.Auto update interupted");
                            LogEvents($"Error occured in stopping the service.Auto update interupted");
                            LogEvents($"State : {status}");
                            await Task.Delay(3000);
                            Application.Exit();
                        }
                    }
                    catch (Exception ex)
                    {
                        DisplayProgress("Error occured in Installation Process.\nClosing the application");
                        LogEvents("Error occured in Installation Process.Closing the application");
                        LogEvents($"State : {status}");
                        LogExceptions(" InitiateAutoUpdateProcess() ", ex);
                        Application.Exit();
                    }

                }
                else
                {
                    DisplayProgress($"Closing the application.");
                    LogEvents($"Closing the application.");
                    await Task.Delay(1500);
                    Application.Exit();
                }
            },
        async (error) =>
        {
            DisplayProgress("Error occured in downloading the installer.\nClosing the application");
            LogEvents($"Error occured in downloading the installer.Closing the application");
            LogEvents($"State : {status}");
            LogExceptions(" InitiateAutoUpdateProcess() ", error);
            Application.Exit();
        });

        }

        private async Task CompleteUpdateProcess()
        {
            try
            {
                DisplayProgress("Service is running...");
                LogEvents($"Service is running...");
                await Task.Delay(2000);
                DisplayProgress("Cleaning up the resources...");
                LogEvents($"Cleaning up the resources...");
                status = InstallationStatus.ServiceUpdateCompleted;
                LogEvents($"State : {status}");
                await Task.Delay(2000);
                await DisposeTempFiles();
            }
            catch (Exception)
            {

                throw;
            }
        }

        private async Task<Respone> DownLoadServiceMSI()
        {
            var response = new Respone { Status = true };
            try
            {
                status = InstallationStatus.ServiceCheck;

                LogEvents($"State : {status}");
                var autoUpdateRequired = await CompareAutoUpdateConfiguration();

                if (autoUpdateRequired.Status)
                {
                    status = InstallationStatus.DownLoading;
                    DisplayProgress($"Downloading new service version {azureServiceVersion}...");
                    LogEvents($"Downloading new service version {azureServiceVersion}...");
                    try
                    {
                        var tempPath = Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi");// @"C:\Arafath\Arafath - 5202024\Arafath\Arafath\Projects\Tranact\EZCashATMService\ezcash_windows_service\EZCashSevice\EZCashATMInstaller\Release\EZCoinDispenserInstaller_2025_08_19.msi";


                        if (!Directory.Exists(TempPath))
                        {
                            Directory.CreateDirectory(TempPath);
                        }

                        var serviceDownLoadURL = Path.Combine(configuration.ServiceDownLoadURL, "EZCoinDispenserInstaller.msi");


                        using (var client = new HttpClient())
                        {
                            byte[] fileBytes = await client.GetByteArrayAsync(serviceDownLoadURL);

                            await File.WriteAllBytesAsync(tempPath, fileBytes);

                            status = InstallationStatus.DownLoadCompleted;
                            LogEvents($"Status : {status}");
                            LogEvents($"Downloaded new service version. Path='{tempPath}'");
                        }

                        var checkSumResponse = await CheckSizeAndSHA256();
                        if (checkSumResponse.Status)
                            return response;
                        else
                        {
                            response.Status = false;
                            response.Error = checkSumResponse.Error;
                            return response;
                        }

                    }
                    catch (Exception ex)
                    {
                        status = InstallationStatus.DownLoadFailed;
                        LogEvents($"State : {status}");
                        LogExceptions(" DownLoadServiceMSI inner. ", ex);
                        response.Status = false;
                        response.Error = ex.Message;
                        return response;
                    }

                }
                else
                {
                    DisplayProgress(autoUpdateRequired.Error);
                    LogEvents(autoUpdateRequired.Error);
                    await Task.Delay(2000);
                    DisplayProgress("Cleaning up the resources...");
                    LogEvents("Cleaning up the resources...");
                    await Task.Delay(2000);
                    await DisposeTempFiles();
                    response.Status = false;
                    response.Error = autoUpdateRequired.Error;
                    return response;
                }
            }
            catch (Exception ex)
            {
                LogExceptions(" DownLoadServiceMSI ", ex);
                DisplayProgress("Error in downloading service.");
                response.Status = false;
                response.Error = ex.Message;
                return response;
            }
        }

        private async Task<Respone> CheckSizeAndSHA256()
        {
            var response = new Respone() { Status = true };
            try
            {
                DisplayProgress("Validating Installer Checksum");
                LogEvents($"Validating Installer Checksum");

                await Task.Delay(1000);
                status = InstallationStatus.CheckDownLoadSizeSH256;
                LogEvents($"State : {status}");

                if (File.Exists(Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi")))
                {
                    FileInfo fi = new(Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi"));

                    if (fi.Length == azureAutoUpdate.Size)
                    {
                        LogEvents($"Installer Size ='{fi.Length}'");
                        var SH256 = GetSHA256(Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi")).ToUpper();
                        if (SH256 == azureAutoUpdate.SH256)
                        {
                            LogEvents($"Installer SH256 ='{SH256}'");
                            response.Status = true;
                            return response;

                        }
                        else
                        {
                            DisplayProgress($"Installer SH256 validation failed.\nSH256 not matching.");
                            LogEvents($"Installer SH256 validation failed.Expected = '{azureAutoUpdate.SH256}', Actual='{SH256}'");
                            await Task.Delay(4000);
                            status = InstallationStatus.CheckDownLoadSizeSH256Failed;
                            LogEvents($"State : {status}");
                            response.Status = false;
                            response.Error = $"Installer SH256 validation failed.\nSH256 not matching.";
                            return response;
                        }
                    }
                    else
                    {
                        DisplayProgress($"Installer Checksum validation failed.\nExpected size={azureAutoUpdate.Size}\nActual={fi.Length}");
                        LogEvents($"Installer size validation failed.Expected size={azureAutoUpdate.Size},Actual={fi.Length}");
                        status = InstallationStatus.CheckDownLoadSizeSH256Failed;
                        LogEvents($"State : {status}");
                        await Task.Delay(4000);
                        response.Status = false;
                        response.Error = $"Installer Checksum validation failed.\nExpected size={azureAutoUpdate.Size}\nActual={fi.Length}";
                        return response;
                    }
                }
                else
                {
                    DisplayProgress($"File Not found in '{Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi")}' ");
                    LogEvents($"File Not found in ' {Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi")} ' ");
                    status = InstallationStatus.CheckDownLoadSizeSH256Failed;
                    LogEvents($"State : {status}");
                    await Task.Delay(4000);
                    response.Status = false;
                    response.Error = $"File Not found in '{Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi")}' ";
                    return response;
                }


            }
            catch (Exception ex)
            {
                LogExceptions(" CheckSizeAndSHA256 ", ex);
                DisplayProgress("Error in validating installer checksum.");
                LogEvents($"State : {status}");
                await Task.Delay(4000);
                response.Status = false;
                response.Error = ex.Message;
                return response;
            }
        }

        private string GetSHA256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                byte[] hashBytes = sha256.ComputeHash(stream);

                // Convert to hex string
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                    sb.Append(b.ToString("x2")); // lowercase hex

                return sb.ToString();
            }
        }

        private async Task<Respone> CompareAutoUpdateConfiguration()
        {
            var updateRresponse = new Respone() { Status = true };
            try
            {
                var tempPath = Path.Combine(TempPath, "AutoUpdateConfig.txt");

                if (!Directory.Exists(TempPath))
                {
                    Directory.CreateDirectory(TempPath);
                }

                var serviceDownLoadURL = Path.Combine(configuration.ServiceDownLoadURL, "AutoUpdateConfig.txt");
                LogEvents($"Downloading Auto update configuration file from {configuration.ServiceDownLoadURL}");

                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(serviceDownLoadURL, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    {
                        azureAutoUpdateConfig = System.Text.Json.JsonSerializer.Deserialize<List<AutoUpdate>>(contentStream);

                        using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            await contentStream.CopyToAsync(fileStream);
                            LogEvents($"Azure Auto update configuration file saved to {tempPath}");
                        }
                    }

                }

                string localautoUpdatefilePath = Path.Combine(configuration.ServiceInstallPath, "AutoUpdateConfig.txt");

                azureAutoUpdate = azureAutoUpdateConfig?.Where(x => x.Name == "EZCashCoin").FirstOrDefault();
                if (azureAutoUpdate != null)
                    azureServiceVersion = azureAutoUpdate?.Version;

                LogEvents($"Verifying local version with azure version...");

                if (File.Exists(localautoUpdatefilePath))
                {
                    string fileContent = File.ReadAllText(localautoUpdatefilePath);

                    localAutoUpdateConfig = System.Text.Json.JsonSerializer.Deserialize<List<AutoUpdate>>(fileContent);

                    if (localAutoUpdateConfig != null && localAutoUpdateConfig.Any())
                    {
                        try
                        {
                            localAutoUpdate = localAutoUpdateConfig.Where(x => x.Name == "EZCashCoin").FirstOrDefault();

                            if (localAutoUpdate != null && azureAutoUpdate != null && !string.IsNullOrEmpty(localAutoUpdate.Version) && !string.IsNullOrEmpty(azureAutoUpdate.Version))
                            {

                                localServiceVersion = localAutoUpdate.Version;
                                var azureversion = azureAutoUpdate.Version.Replace('.', ' ').Replace(" ", "");
                                var localVersion = localAutoUpdate.Version.Replace('.', ' ').Replace(" ", "");

                                var azureIntVersion = Int32.TryParse(azureversion, out Int32 azureResult);
                                var localIntVersion = Int32.TryParse(localVersion, out Int32 localResult);
                                LogEvents($"Local version : {localAutoUpdate.Version} , Azure Version : {azureAutoUpdate.Version}");

                                if (localIntVersion && azureIntVersion && azureResult > localResult)
                                {
                                    LogEvents($"Prompting confirmation to continue update.");
                                    DialogResult result = MessageBox.Show(
                                                              $"New Version {azureServiceVersion} available for download.\n Do you want to continue?",   // Message text
                                                              "Confirmation",              // Title of the MessageBox
                                                              MessageBoxButtons.OKCancel,  // Buttons to display
                                                              MessageBoxIcon.Question      // Icon (optional)
                                                          );

                                    if (result == DialogResult.OK)
                                    {
                                        LogEvents($"Ok selected to continue the update process.");
                                        return updateRresponse;
                                    }
                                    else
                                    {
                                        LogEvents($"Cancel selected.Cancelling the update process.");
                                        updateRresponse.Status = false;
                                        updateRresponse.Error = $"Cancelling the update process.";
                                        return updateRresponse;
                                    }
                                }
                                else if (localIntVersion && azureIntVersion && azureResult == localResult)
                                {
                                    LogEvents($"Service version {localServiceVersion} is already up to date.No updates needed.");
                                    DisplayProgress($"Your service version {localServiceVersion} is already up to date.\nNo updates needed.");
                                    updateRresponse.Status = false;
                                    updateRresponse.Error = $"Service version {localServiceVersion} is already up to date.\nNo updates needed.";
                                    return updateRresponse;
                                }
                                else if (localIntVersion && azureIntVersion && azureResult < localResult)
                                {
                                    LogEvents($"Prompting confirmation to continue update.");
                                    DialogResult result = MessageBox.Show(
                                                              $"Lower Version {azureServiceVersion} available for download.\n Do you want to downgrade the service?",   // Message text
                                                              "Confirmation",              // Title of the MessageBox
                                                              MessageBoxButtons.OKCancel,  // Buttons to display
                                                              MessageBoxIcon.Question      // Icon (optional)
                                                          );

                                    if (result == DialogResult.OK)
                                    {
                                        LogEvents($"Ok selected to continue the downgrade process.");

                                        return updateRresponse;
                                    }
                                    else
                                    {
                                        LogEvents($"Cancel selected.Cancelling the update process.");
                                        updateRresponse.Status = false;
                                        updateRresponse.Error = $"Cancelling the update process.";
                                        return updateRresponse;
                                    }
                                }
                                return updateRresponse;
                            }
                            else
                            {
                                updateRresponse.Status = false;
                                updateRresponse.Error = "Invalid data found in Local/Azure Auto update configuration file.";
                                return updateRresponse;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogExceptions("CompareAutoUpdateConfiguration in reading local/azure configuration", ex);
                            updateRresponse.Status = false;
                            updateRresponse.Error = ex.Message;
                            return updateRresponse;
                        }
                    }
                    else
                    {
                        return updateRresponse;
                    }
                }
                else
                {
                    LogEvents($"Local version not found. Initiating service installer process....");
                    return updateRresponse;
                }
            }
            catch (Exception ex)
            {
                LogExceptions("CompareAutoUpdateConfiguration in whole.", ex);
                updateRresponse.Status = false;
                updateRresponse.Error = ex.Message;
                return updateRresponse;

            }
        }

        private bool IsServiceInstalled(string serviceName)
        {
            // Get all installed services
            ServiceController[] services = ServiceController.GetServices();

            // Check if a service with the given name exists
            return services.Any(s => s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
        }

        private bool CopyConfigToServiceFolder()
        {
            try
            {
                if (Directory.Exists(TempPath))
                {
                    string sourceFilePath = Path.Combine(TempPath, "CoinDispenserService.exe.config");

                    string sourceAutoUpdateConfig = System.Text.Json.JsonSerializer.Serialize(azureAutoUpdateConfig);
                    string destinationAutoUpdateFilePath = Path.Combine(configuration.ServiceInstallPath, "AutoUpdateConfig.txt");
                    File.WriteAllText(destinationAutoUpdateFilePath, sourceAutoUpdateConfig);

                    LogEvents($"Auto update config file copied to '{destinationAutoUpdateFilePath}' for future update.");
                    if (!File.Exists(sourceFilePath))
                        return true;

                    string destinationFilePath = Path.Combine(configuration.ServiceInstallPath, Path.GetFileName(sourceFilePath));


                    File.Copy(sourceFilePath, destinationFilePath, true);
                    LogEvents($"Original EZCash config file copied to '{destinationAutoUpdateFilePath}' for service startup.");
                    return true;
                }
                else
                    return false;
            }
            catch (Exception ex)
            {
                LogExceptions(" CopyConfigToServiceFolder() ", ex);
                return false;
            }
        }

        private async Task<bool> UnistallService()
        {
            try
            {

                string uninstallString = FindUninstallString(appName);

                if (!string.IsNullOrEmpty(uninstallString))
                {
                    LogEvents($"UninstallString for {appName} : '{uninstallString}'");
                    LogEvents($"Extracting product code for '{uninstallString}'");

                    // Extract product code (GUID) from uninstall string
                    string productCode = ExtractProductCode(uninstallString);

                    if (!string.IsNullOrEmpty(productCode))
                    {
                        LogEvents($"Product code for '{uninstallString}' is '{productCode}'");
                        // Build silent uninstall command
                        string silentUninstall = $"msiexec.exe /x {productCode} /qn";

                        LogEvents($"Starting silent service uninstall process...");

                        ProcessStartInfo processStartInfo = new()
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/c {silentUninstall}",
                            Verb = "runas",  // Run as admin
                            UseShellExecute = true,
                            CreateNoWindow = true
                        };

                        using Process process = Process.Start(processStartInfo);
                        process?.WaitForExit();
                        if (process?.ExitCode == 0)
                        {
                            LogEvents($"Installer un-installed successfully.");
                        }
                        else
                        {
                            LogEvents($"Installer un-install failed. Process exit with code '{process.ExitCode}'");
                        }
                    }
                    else
                    {
                        DisplayProgress("Could not extract product code from uninstall string.");
                        LogEvents($"Could not extract product code from uninstall string.");
                    }
                }
                else
                {
                    LogEvents($"Application not found in uninstall registry. Deleting created service {appName}");
                    DisplayProgress($"Application not found in uninstall registry.\nDeleting created service {appName}");

                    DeleteExistingServiceIfInstalledThroughCmd();
                }

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private string FindUninstallString(string displayName)
        {
            string[] registryPaths =
            {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

            foreach (var path in registryPaths)
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(path))
                {
                    if (key == null) continue;

                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using (RegistryKey subkey = key.OpenSubKey(subkeyName))
                        {
                            var name = subkey?.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(name) && name.Contains(displayName))
                            {
                                return subkey.GetValue("UninstallString") as string;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private string ExtractProductCode(string uninstallString)
        {
            // Usually looks like: MsiExec.exe /I{GUID} or /X{GUID}
            int start = uninstallString.IndexOf('{');
            int end = uninstallString.IndexOf('}');
            if (start >= 0 && end > start)
            {
                return uninstallString.Substring(start, end - start + 1);
            }
            return null;
        }

        private async Task<bool> InstallNewEzCashService()
        {
            try
            {
                var tempPath = Path.Combine(TempPath, $"EZCoinDispenserInstaller_{azureServiceVersion.Replace('.', '_')}.msi");

                LogEvents($"Silent installation process started for EZCoinDispenserInstaller version '{azureServiceVersion}'");
                ProcessStartInfo processStartInfo = new()
                {
                    FileName = "msiexec.exe",
                    UseShellExecute = false,
                    Verb = "runas",  // Run as admin
                    CreateNoWindow = true
                };

                processStartInfo.ArgumentList.Add("/i");
                processStartInfo.ArgumentList.Add(tempPath);
                processStartInfo.ArgumentList.Add("/qb");
                processStartInfo.ArgumentList.Add("/norestart");

                using Process process = Process.Start(processStartInfo);

                process?.WaitForExit();

                if (process?.ExitCode == 0)
                {
                    LogEvents($"Installer installed successfully.Process exit with code '0'");
                    return true;
                }
                else
                {
                    LogEvents($"Installer failed. Process exit with code '{process.ExitCode}'");
                    return false;
                }

            }
            catch (Exception)
            {
                return false;
            }

        }


        private async Task<bool> StopService()
        {
            try
            {
                LogEvents($"Checking service status.");
                var installed = IsServiceInstalled(ServiceName);
                if (!installed)
                {
                    LogEvents($"Service installed.");
                    return true;
                }

                using (ServiceController sc = new(ServiceName))
                {
                    if (sc.Status != ServiceControllerStatus.Stopped)
                    {
                        LogEvents($"Service state : {sc.Status}. Stopping the service");
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(1));
                        LogEvents($"Service state : {sc.Status}.");

                    }
                }

                await Task.Delay(2000);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        private async Task<bool> StartService()
        {
            try
            {

                using (ServiceController sc = new(ServiceName))
                {
                    if (sc.Status == ServiceControllerStatus.Stopped)
                    {
                        LogEvents($"Service State : {sc.Status}. Starting the service...");
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running);
                        LogEvents($"Service State : {sc.Status}.");
                    }
                }

                await Task.Delay(2000);

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private bool DeleteExistingServiceIfInstalledThroughCmd()
        {
            try
            {
                var binPath = Path.Combine(TempPath, "EZCashSevice.exe");
                string arguments = $"delete \"{ServiceName}\" ";

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "sc.exe", // The executable for Service Control Manager
                    Arguments = arguments,
                    UseShellExecute = true, // Required to run with elevated privileges if needed
                    Verb = "runas", // Prompts for administrator privileges
                    CreateNoWindow = true // Prevents a command prompt window from appearing
                };

                using (Process process = Process.Start(startInfo))
                {
                    process.WaitForExit(); // Wait for the sc.exe command to complete
                    if (process.ExitCode == 0)
                    {
                        LogEvents($"Service '{ServiceName}' deleted successfully.");
                        DisplayProgress($"Service '{ServiceName}' deleted successfully.");
                    }
                    else
                    {
                        LogEvents($"Failed to delete service '{ServiceName}'. Exit code: {process.ExitCode}");
                        DisplayProgress($"Failed to delete service '{ServiceName}'. Exit code: {process.ExitCode}");
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> ReinstalServiceOnErroredProcess()
        {
            try
            {
                DisplayProgress("Checking service status...");
                LogEvents($"Checking service status...");
                await Task.Delay(2000);
                var isServiceInstalled = IsServiceInstalled(ServiceName);

                if (isServiceInstalled)
                {
                    LogEvents($"Service is already installed.");
                    DisplayProgress($"Copying existing service files to {configuration.ServiceInstallPath} ...");
                    LogEvents($"Copying existing service files to {configuration.ServiceInstallPath} ...");
                    await Task.Delay(3000);
                    var configCopyResult = CopyConfigToServiceFolder();
                    if (configCopyResult)
                    {
                        DisplayProgress($"Copying existing service files is completed.");
                        LogEvents($"Copying existing service files is completed.");

                        await Task.Delay(1500);
                        DisplayProgress("Starting the existing service ...");
                        LogEvents("Starting the existing service ...");
                        LogEvents($"State : {status}");
                        status = InstallationStatus.ServiceStarting;
                        await Task.Delay(3000);
                        var startResult = await StartService();

                        if (startResult)
                        {
                            status = InstallationStatus.ServiceStarted;
                            await CompleteUpdateProcess();
                        }
                        else
                        {
                            LogEvents("Something went wrong in starting existing service.Manual start needed");

                            DisplayProgress("Something went wrong in starting existing service.\nManual start needed");
                        }

                        return true;
                    }
                    else
                    {
                        await CompleteUpdateProcess();
                        return false;
                    }
                }

                else
                {
                    status = InstallationStatus.ServiceFilesCopying;

                    var copyResult = await CopyEZCashserviceFiles(TempPath, configuration.ServiceInstallPath);
                    if (copyResult)
                    {
                        status = InstallationStatus.ServiceFilesCopyingCompleted;
                        var binPath = Path.Combine(configuration.ServiceInstallPath, "EZCashSevice.exe");
                        string arguments = $"create \"{ServiceName}\" binPath= \"{binPath}\"";

                        status = InstallationStatus.ServiceCreating;
                        ProcessStartInfo startInfo = new ProcessStartInfo
                        {
                            FileName = "sc.exe", // The executable for Service Control Manager
                            Arguments = arguments,
                            UseShellExecute = true, // Required to run with elevated privileges if needed
                            Verb = "runas", // Prompts for administrator privileges
                            CreateNoWindow = true // Prevents a command prompt window from appearing
                        };

                        using (Process process = Process.Start(startInfo))
                        {
                            process.WaitForExit(); // Wait for the sc.exe command to complete
                            if (process.ExitCode == 0)
                            {
                                LogEvents($"Service '{ServiceName}' created successfully.");
                                DisplayProgress($"Service '{ServiceName}' created successfully."); //"Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                                status = InstallationStatus.ServiceCreated;
                                await Task.Delay(2000);
                                DisplayProgress("Starting the new service ...");
                                LogEvents("Starting the new service ...");
                                await Task.Delay(3000);
                                status = InstallationStatus.ServiceStarting;
                                var startResult = await StartService();

                                if (startResult)
                                {
                                    status = InstallationStatus.ServiceStarted;
                                    await CompleteUpdateProcess();
                                }
                                else
                                {
                                    DisplayProgress("Something went wrong in starting new service.Manual start needed");
                                    LogEvents("Something went wrong in starting new service.Manual start needed");
                                }
                            }
                            else
                            {
                                LogEvents($"Failed to create service '{ServiceName}'.\n Exit code: {process.ExitCode}");
                                DisplayProgress($"Failed to create service '{ServiceName}'.\n Exit code: {process.ExitCode}");//, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }

                        return true;
                    }
                    else
                    {
                        DisplayProgress("Failed to copy files from Temp folder to EZCash folder");
                        LogEvents("Failed to copy files from Temp folder to EZCash folder");
                        await Task.Delay(2000);
                        DisplayProgress("Cleaning up the resources...");
                        LogEvents("Cleaning up the resources...");
                        await Task.Delay(2000);
                        await DisposeTempFiles();
                        return false;
                    }
                }


            }
            catch (Exception ex)
            {
                LogExceptions(" ReinstalServiceOnErroredProcess ", ex);
                return false;
            }
        }

        private async Task DisposeTempFiles()
        {
            try
            {
                if (Directory.Exists(TempPath))
                    Directory.Delete(TempPath, true);

                LogEvents("Update Process completed.");
                DisplayProgress("Update Process completed.");
                await Task.Delay(3000);
                LogEvents("Closing Auto Update application.");
                Application.Exit();
            }
            catch (Exception)
            {
                LogEvents("Update Process completed.");
                DisplayProgress("Update Process completed.");
                await Task.Delay(3000);
                LogEvents("Closing Auto Update application.");
                Application.Exit();
            }
        }

        private void DisplayProgress(string message)
        {
            lblStatus.BeginInvoke((Action)(() =>
            {
                lblStatus.Text = message;

                lblStatus.Left = (lblStatus.Parent.ClientSize.Width - lblStatus.Width) / 2;
                lblStatus.Top = (lblStatus.Parent.ClientSize.Height - lblStatus.Height) / 2;
            }));
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            LogEvents("Clsoing Auto Update application.");
            Application.Exit();
        }

        private void btnFolderSelect_Click(object sender, EventArgs e)
        {

            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                // Optional: Set initial properties
                folderBrowserDialog.Description = "Select a folder for your files:";
                folderBrowserDialog.ShowNewFolderButton = true;

                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = folderBrowserDialog.SelectedPath;
                    txtServiceInstallPath.Text = selectedPath;
                    LogEvents($"Service Install Folder Path = '{selectedPath}'");
                }
            }

        }

        private void LogEvents(string input)
        {
            Logger.LogWithNoLock($" {input}");
        }

        private void LogExceptions(string message, Exception ex)
        {
            Logger.LogExceptionWithNoLock($" Exception at {message}", ex);
        }

        private async void AutoUpdater_LoadAsync(object sender, EventArgs e)
        {
            try
            {
                LogEvents($"Version 1.0.0");
                LogEvents($"EZCash service auto update process started.");

                TempPath = Path.Combine(Path.GetTempPath(), "EZCash");

                var executablePath = Path.GetDirectoryName(Application.ExecutablePath);

                var configPath = Path.Combine(executablePath, "UpdateEZCashCoinServiceConfig.config");
                if (File.Exists(configPath))
                {
                    LogEvents($"UpdateEZCashServiceConfig.config found.Reading configuration information.");
                    DisplayProgress("Checking service update...");

                    await Task.Delay(2000);

                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(UpdateEZCashServiceConfiguration));

                    using (var reader = new StreamReader(configPath))
                    {
                        configuration = (UpdateEZCashServiceConfiguration)xmlSerializer.Deserialize(reader);

                        if (configuration != null && !string.IsNullOrEmpty(configuration.ServiceDownLoadURL) && !string.IsNullOrEmpty(configuration.ServiceInstallPath))
                        {
                            await InitiateAutoUpdateProcess();

                        }
                        else
                        {
                            LogEvents("Either config is null Or Version/Service download URL is empty.");
                            MessageBox.Show($"Either config is null Or Version/Service download URL is empty.");
                            Application.Exit();

                        }
                    }
                }
                else
                {
                    LogEvents($"UpdateEZCashServiceConfig.config not found.Loading controls to get config information.");
                    lblService.Visible = true;
                    lblVersion.Visible = true;
                    txtServiceInstallPath.Visible = true;
                    txtServiceDownloadURL.Visible = true;
                    btnSaveConfiguration.Visible = true;
                    btnCancel.Visible = true;
                    btnFolderSelect.Visible = true;

                }
            }
            catch (Exception ex)
            {
                LogExceptions(" frmAutoUpdater_Load ", ex);
                MessageBox.Show($"Error Occured: {ex.Message}");
            }
        }
    }

    class ServiceHelper
    {
        public static string GetServicePath(string serviceName)
        {
            try
            {
                string key = $@"SYSTEM\CurrentControlSet\Services\{serviceName}";
                using (RegistryKey rk = Registry.LocalMachine.OpenSubKey(key))
                {
                    if (rk != null)
                    {
                        string imagePath = rk.GetValue("ImagePath").ToString();
                        // Expand %SystemRoot% and quotes if present
                        return Environment.ExpandEnvironmentVariables(imagePath).Trim('"');
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                return string.Empty;
            }

        }
    }

    [Serializable]
    public class UpdateEZCashServiceConfiguration
    {
        public string ServiceInstallPath { get; set; }
        public string ServiceDownLoadURL { get; set; }
    }


    public class AutoUpdate
    {
        public string Version { get; set; }
        public string Name { get; set; }
        public long Size { get; set; }
        public string SH256 { get; set; }
    }

    public class Respone
    {
        public bool Status { get; set; }
        public string Error { get; set; }
    }
}
