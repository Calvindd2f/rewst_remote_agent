#!/usr/bin/python3

import argparse
import asyncio
import httpx
import json
import logging
import os
import platform
import psutil
from concurrent.futures import ThreadPoolExecutor
from azure.iot.device.aio import IoTHubDeviceClient

# Set the status update interval and get the operating system type
status_update_checkin_time = 600
os_type = platform.system()
executor = ThreadPoolExecutor()
logging.basicConfig(level=logging.INFO)

# Function to send a status update to the IoT Hub
async def send_status_update():
    # Collect status data
    status_data = {
        "cpu_usage": psutil.cpu_percent(interval=1),
        "memory_usage": psutil.virtual_memory().percent
    }
    # Create message object
    message_json = json.dumps(status_data)
    # Send message
    print("Sending status update to IoT Hub...")
    await device_client.send_message(message_json)
    print("Status update sent!")

# Function to load configuration from a file
def load_config():
    try:
        with open('config.json') as f:
            config = json.load(f)
    except Exception as e:
        logging.error(f"Error: {e}")
        return None
    # Check for required keys in the configuration
    required_keys = [
        'azure_iot_hub_host',
        'device_id',
        'shared_access_key',
        'rewst_engine_host',
        'rewst_org_id'
    ]
    for key in required_keys:
        if key not in config:
            logging.error(f"Error: Missing '{key}' in configuration.")
            return None
    return config

# Function to construct a connection string from the configuration
def get_connection_string(config):
    conn_str = (
        f"HostName={config['azure_iot_hub_host']};"
        f"DeviceId={config['device_id']};"
        f"SharedAccessKey={config['shared_access_key']}"
    )
    return conn_str

# Handler function for messages received from the IoT Hub
def message_handler(message):
    print(f"Received message: {message.data}")
    message_data = json.loads(message.data)
    if "commands" in message_data:
        commands = message_data["commands"]
        post_id = message_data.get("post_id")  # Get post_id, if present
        interpreter_delimiter = message_data.get("interpreter_delimiter", "\n")  # Get delimiter, if present
        print("Running commands")
        executor.submit(run_handle_commands, commands, post_id, None, None, interpreter_delimiter)

# Function to handle the execution of commands
def run_handle_commands(commands, post_id=None, rewst_engine_host=None, interpreter=None, interpreter_delimiter="\n"):
    asyncio.run(handle_commands(commands, post_id, rewst_engine_host, interpreter, interpreter_delimiter))

# Async function to execute the list of commands
async def execute_commands(commands, post_url=None, interpreter_override=None, interpreter_delimiter="\n"):
    # Determine the interpreter based on the operating system
    if os_type == 'windows':
        default_interpreter = 'powershell'
    elif os_type == 'darwin':
        default_interpreter = '/bin/zsh'
    else:
        default_interpreter = '/bin/bash'
    interpreter = interpreter_override or default_interpreter
    # Join commands using the specified delimiter
    
    
    # If PowerShell is the interpreter, update the commands to include the post_url variable
    if "powershell" in interpreter:
        preamble = (
            f"[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12\n"
            f"$post_url = '{post_url}'\n"
        )
        # Prepend the preamble to the commands
        commands = preamble + commands

    # Prepare the command for execution
    if interpreter_override:
        # When an interpreter override is specified, prefix the commands with the interpreter
        command = f'{interpreter} -c "{commands}"'
    else:
        # When using the default interpreter, no need to prefix the commands
        command = commands# Create the command string
    command = f'{interpreter} -c "{all_commands}"'
    # Execute the command
    process = await asyncio.create_subprocess_shell(
        command,
        stdout=asyncio.subprocess.PIPE,
        stderr=asyncio.subprocess.PIPE,
    )
    # Gather output
    stdout, stderr = await process.communicate()
    # Decode output from binary to text
    stdout = stdout.decode('utf-8')
    return stdout.strip()

    # Attempt to parse the command output as JSON
    try:
        message_data = json.loads(command_output)
    except json.JSONDecodeError as e:
        print(f"Error decoding command output as JSON: {e}")
        message_data = {"error": f"Error decoding command output as JSON: {e}", "output": command_output}
    
    if "powershell" not in interpreter:
        print("Sending Results to Rewst via httpx.")
        # Send POST request with command results
        async with httpx.AsyncClient() as client:
            response = await client.post(post_url, json=message_data)
            print(f"POST request status: {response.status_code}")
            if response.status_code != 200:
                # Log error information if the request fails
                print(f"Error response: {response.text}")


# Async function to handle the execution of commands and send the output to IoT Hub
async def handle_commands(commands, post_id=None, rewst_engine_host=None, interpreter_override=None, interpreter_delimiter="\n"):    
    if post_id:
        post_path = post_id.replace(":", "/")
        post_url = f"https://{rewst_engine_host}/webhooks/custom/action/{post_path}"
    # Execute the commands
    command_output = await execute_commands(commands, rewst_engine_host, interpreter_override, interpreter_delimiter)
    try:
        # Try to parse the output as JSON
        message_data = json.loads(command_output)
    except json.JSONDecodeError as e:
        print(f"Error decoding command output as JSON: {e}")
        message_data = {"error": f"Error decoding command output as JSON: {e}", "output": command_output}
    # Send the command output to IoT Hub
    message_json = json.dumps(message_data)
    await device_client.send_message(message_json)
    print("Message sent!")

# Main async function
async def main(check_mode=False):
    global rewst_engine_host
    config_data = load_config()
    if config_data is None:
        exit(1)
    connection_string = get_connection_string(config_data)
    rewst_engine_host = config_data['rewst_engine_host']
    rewst_org_id = config_data['rewst_org_id']
    global device_client
    device_client = IoTHubDeviceClient.create_from_connection_string(connection_string)
    print("Connecting to IoT Hub...")
    await device_client.connect()
    print("Connected!")
    if check_mode:
        # Check mode for testing communication
        print("Check mode: Sending a test message...")
        e = None
        try:
            await device_client.send_message(json.dumps({"test_message": "Test message from device"}))
            print("Check mode: Communication test successful. Test message sent.")
        except Exception as ex:
            e = ex
            print(f"Check mode: Communication test failed. Could not send test message: {e}")
        finally:
            await device_client.disconnect()
            exit(0 if not e else 1)
    else:
        # Set the message handler and start the status update task
        device_client.on_message_received = message_handler
        async def status_update_task():
            while True:
                await send_status_update()
                await asyncio.sleep(status_update_checkin_time)
        status_update_task = asyncio.create_task(status_update_task())
        stop_event = asyncio.Event()
        await stop_event.wait()

# Entry point of the script
if __name__ == "__main__":
    parser = argparse.ArgumentParser(description='Run the IoT Hub device client.')
    parser.add_argument('--check', action='store_true', help='Run in check mode to test communication')
    args = parser.parse_args()
    asyncio.run(main(check_mode=args.check))
