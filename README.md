# rewst_remote_agent C# - status

**Completed Conversions:**

1. rewst_agent_config.py → RewstAgentConfig.cs


- Successfully converted the configuration tool
- Implemented proper dependency injection
- Added robust error handling
- Maintained cross-platform compatibility
- Enhanced async/await patterns


2. rewst_windows_service.py → RewstWindowsService.cs and ServiceManager.cs


- Converted the Windows service implementation
- Added comprehensive service management across all platforms
- Improved process management and monitoring
- Enhanced logging and error handling
- Added proper lifecycle management

**Supporting Components:**

1. Checksum validation (verify_application_checksum.py)


- Implemented in C# with enhanced security features
- Added proper async file operations
- Improved error handling


2. Organization ID extraction


- Implemented as a utility function
- Enhanced with better pattern matching
- Added proper error handling

**Still Pending:**

1. iot_hub_module Conversion


Authentication mechanisms
Connection management
Message handling
Error handling
Logging setup


2. config_module Conversion


Configuration I/O operations
Configuration fetching
Host information collection

---

Next Steps:

1. Convert the IoT Hub module, focusing on:

- Maintaining the abstraction layers
- Implementing proper Azure SDK integration
- Ensuring robust message handling


2. Implement the configuration module, ensuring:

- Proper file operations
- Secure configuration handling
- Cross-platform compatibility


3. Create comprehensive integration tests to verify:

- Cross-platform functionality
- Service lifecycle management
- Configuration handling
- IoT Hub connectivity

The converted codebase has several improvements over the Python version:

- Strong typing throughout
- Better dependency management
- Enhanced error handling
- More robust async operations
- Better service lifecycle management
- Improved logging infrastructure

---

# rewst_remote_agent

[![Unit Tests](https://github.com/RewstApp/rewst_remote_agent/actions/workflows/unit-tests.yml/badge.svg)](https://github.com/RewstApp/rewst_remote_agent/actions/workflows/unit-tests.yml) [![Code Coverage](https://github.com/RewstApp/rewst_remote_agent/actions/workflows/coverage.yml/badge.svg)](https://github.com/RewstApp/rewst_remote_agent/actions/workflows/coverage.yml)

An RMM-agnostic remote agent using the Azure IoT Hub

Goals:
* Run as an service on Windows (Linux / Mac coming later!)
* Provisioning (Windows):
  * `iwr ((irm {{ github_release_url }}).assets|?{$_.name -eq "rewst_agent_config.win.exe"}|select -exp browser_download_url) -OutFile rewst_agent_config.win.exe`
    * Downloads latest release of configuration Utility from GitHub
  * `.\rewst_agent_config.win.exe` `--config-url` _{ Your Trigger URL }_  `--config-secret` _{ Your global config secret }_ `--org-id` _{ customer organization id }_
    * Initiates configuration and installation of the agent
    * `config-url`: The configured workflow trigger from the Crate installation
    * `config-secret`: Stored in an Org variable under your company. If it changes, existing installations will still work, but new commands to install it will need the new secret.
    * `org-id`: The organization's (your customer) Rewst Org ID.
* Operation:
    * Stays resident and connected to the IoT Hub
    * Rewst workflows can send an object to IoT hub that contains a list of `commands`
    * When the list arrives, the script will spawn shell process and process these commands sequentially within the _same_ environment
    * Each command will have its output collected and returned back in a list of `command_results` that is in the same index as the command from `commands`
    * Handle disconnects gracefully and restart


