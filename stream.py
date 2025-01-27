"""\
---------------------
The MIT License (MIT)

Copyright (c) 2012-2014 Sungeun K. Jeon
Copyright (c) 2025 James Dalessandro

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.
---------------------
"""

import serial
import argparse
import time
import sys

RX_BUFFER_SIZE = 128

# Define command line argument interface
parser = argparse.ArgumentParser(description='Stream g-code file to grbl. (pySerial and argparse libraries required)')
parser.add_argument('gcode_file', type=argparse.FileType('r'), help='g-code filename to be streamed')
parser.add_argument('device_file', help='serial device path')
parser.add_argument('-q', '--quiet', action='store_true', default=False, help='suppress output text')
args = parser.parse_args()

s = None
try:
    # Initialize serial connection
    s = serial.Serial(args.device_file, 115200)
    f = args.gcode_file
    verbose = not args.quiet

    # Wake up GRBL
    print("Initializing grbl...")
    s.write(b"\r\n\r\n")
    time.sleep(2)
    s.flushInput()

    # Stream G-code to GRBL
    l_count = 0
    g_count = 0
    c_line = []
    for line in f:
        l_count += 1
        l_block = line.strip()

        # Special handling for system commands
        if l_block.startswith('$H') or l_block.startswith('$SLP'):
            print(f"Sending system command: {l_block}")
            print("Waiting for idle state to execute system command...")
            while True:
                s.write(b"?")  # Request GRBL status
                status = s.readline().decode('utf-8').strip()
                if '<Idle' in status:  # Ensure GRBL is idle before system commands
                    break

            s.write((l_block + '\n').encode('utf-8'))  # Send the system command
            response = s.readline().decode('utf-8').strip()
            print(f"System command response: {response}")
            continue

        # Standard G-code processing
        c_line.append(len(l_block) + 1)
        grbl_out = ''
        while sum(c_line) >= RX_BUFFER_SIZE - 1 or s.in_waiting:
            out_temp = s.readline().strip().decode('utf-8')
            if 'ok' not in out_temp and 'error' not in out_temp:
                print("  Debug: ", out_temp)
            else:
                grbl_out += out_temp
                g_count += 1
                grbl_out += str(g_count)
                del c_line[0]
        if verbose:
            print(f"{l_count-1} >>> {l_block}", end=' ')
        s.write((l_block + '\n').encode('utf-8'))
        if verbose:
            print(f"--- BUF: {sum(c_line)} REC: {grbl_out}")

    # G-code streaming completed
    print("\nG-code streaming finished!")
    print("Remember: Always wait until GRBL completes buffered G-code blocks before exiting.\n")

except Exception as e:
    print(f"Error: {e}")
finally:
    # Ensure resources are released
    if s and s.is_open:
        print("Final GRBL status check...")
        while True:
            s.write(b"?")  # Request GRBL status
            status = s.readline().decode('utf-8').strip()
            print(f"Status: {status}")
            if '<Idle' in status:  # Wait for GRBL to report Idle state
                break

        print("GRBL has finished processing.\n")

        # Soft reset to clear GRBL state and buffers
        s.write(b"\x18")  # Ctrl+X sends a soft reset to GRBL
        time.sleep(1)  # Allow time for GRBL to reset
        s.close()
        print("Serial port closed.")
    if args.gcode_file:
        args.gcode_file.close()
