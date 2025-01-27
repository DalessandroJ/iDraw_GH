![iDraw_GH Banner](resources/idraw_gh_titlepic.png)

# iDraw_GH

A single-component Grasshopper plugin for sending GRBL and G-code commands to iDraw pen plotters. With this plugin, you can easily stream commands to your iDraw plotter directly from Grasshopper.

## Features

- **Single-component** for Grasshopper — keeps your workflow simple.  
- **Automated iDraw detection** via GRBL query.  
- **File output** of G-code with timestamped filenames.
- **No Memory Buffer Overflow or Stuttering** due to commands being streamed to the controller via the [character counting method](https://github.com/grbl/grbl/wiki/Interfacing-with-Grbl#streaming-a-g-code-program-to-grbl).

## Installation

1. Download the `iDraw_GH.gha` file from the repository.  
2. Place the plugin file in your Grasshopper `Libraries` folder.  
   - Typically found at:  
     - **Windows:** `C:\Users\<USERNAME>\AppData\Roaming\Grasshopper\Libraries`  
3. Restart Rhino and Grasshopper.  

## Component Inputs

1. **Run** (`Boolean`)  
   - Attach a **Button**  
   - Click/activate to run the component and stream commands to iDraw.  

2. **Commands** (`List of Strings`)  
   - A list of G-code and GRBL commands (e.g., `G1 X10 Y10 F1000` or `$H`, `$SLP`, etc).  
   - Note: The iDraw "DrawCore" controller software does not recognize `G2` or `G3`, so use numerous `G1` moves for arcs and smooth curves (as approximations).  

3. **G-code Folder** (`Text`)  
   - Specify a folder path in which G-code files will be saved.  

4. **Python Path** (`Text`)  
   - Path to your Python 3 executable (`python.exe`).  
   - Developed and tested with Python 3.11.  

## How It Works

1. **Validate Python Path**  
   - The component checks whether the Python executable you provided actually exists.  

2. **Validate/Create G-code Folder**  
   - Verifies that the folder for saving your G-code file exists, and attempts to create it if missing.  

3. **Find iDraw USB Port**  
   - The component queries all connected devices with the GRBL `"$I"` command.  
   - It looks for a response containing `DrawCore`.  
   - If multiple iDraw devices are connected, the first it finds is used.  

4. **Write G-code File**  
   - Your list of G-code commands is saved to a file in the specified folder.  
   - The file name includes a timestamp for easy tracking.  

5. **Stream Commands to iDraw**  
   - The commands are then streamed in order to your iDraw plotter over the detected USB port.  

## Troubleshooting

- **Python path not found?**  
  - Make sure you have installed Python 3.11 (or a compatible version) and update the path in Grasshopper.  
- **Multiple iDraw devices?**  
  - The plugin will use the first iDraw found. Ensure only one device is connected if you want to avoid confusion. If this is actually a problem someone runs into, submit a pull request or an raise an issue.  
- **G2/G3 commands ignored?**  
  - The iDraw "DrawCore" controller **does not** support arcs. Convert arcs and smooth curves to `G1` line segments.  

## Contributing

Feel free to open pull requests or file issues if you find bugs or have feature requests. Contributions are always welcome!


Enjoy plotting with iDraw_GH!
