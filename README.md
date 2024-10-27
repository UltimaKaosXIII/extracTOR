# extractTOR
![image](https://github.com/user-attachments/assets/ba288e4e-cbf3-4d8b-9781-f084bb836331)

## Why another Extractor?
Because Slicers GUI has, for a long while, been unstable.
There have been multiple instances of people needing help to install and configure Slicers GUI. This extractor aims to simplify that.
I, and some others, have also had problems getting the most recent PTS (7.6 #4) files extracted with Slicers GUI so out of frustration and desperation to update my [Terrain Extractor](https://github.com/UltimaKaosXIII/STE2), I made this.

This tool is ***VERY*** early in development and I haven't had a chance to test ***EVERYTHING*** yet.
There may be Bugs, System crashes, Out of Memory Exceptions, Blue Screen of Death crashes, Flying pigs or it may cause you to spontaneously catch fire.
I don't know, I make no promises and I will not be held liable for any damage to you, your house, your pc, your personal files, your refrigerator or your cat/dog/other beloved pet.

This tool **DOES** connect to the internet to get the latest hash list information from jedipedia and **ONLY** to get the latest hash list information from Jedipedia.

The latest release can be found [here](https://github.com/UltimaKaosXIII/extracTOR/releases)

------------------------------------------------------------------

## The first time you run extracTOR:
You'll probably get the typical message about Protecting your PC. It's fine. Click More Info, and then click Run Anyway.

Expect to see a message saying it can't find a hash list.
![image](https://github.com/user-attachments/assets/c61b08e7-827b-4b98-8250-491e0a8b5463)

This is fine, simply click OK, then click either the "Check Hash List" button or the "Download Hash List" button
If you have a "hashes_filename.txt" hash list from an install of Slicers GUI or saved from Jedipedia (https://swtor.jedipedia.net/en/file-names), you can place it in the extracTOR folder and it will be converted to "hashes.bin"

## Runnnig extracTOR:
- If you have no internet connection:
   - If you have no internet connection **AND** you have no hash list, extracTOR will not let you extract, check hash lists **OR** download a hash list.
     - There ***should not*** be any catastrophic crashes in this case, but I can't catch everything on my own, so let me know.

- If you have no internet connection ***AND*** you have no "hashes.bin" file **but** you ***DO*** have a "hashes_filename.txt":
   - Put the "hashes_filename.txt" in the extracTOR folder and it will be converted to "hashes.bin" when you extract.

- If you have no internet connection ***AND*** you ***DO*** have a "hashes.bin":
   - Extraction should work perfectly fine.

## The UI:
![image](https://github.com/user-attachments/assets/ba288e4e-cbf3-4d8b-9781-f084bb836331)
### Assets Folder:
This is where SWTOR stores the Asset Archives. The .tor files.
- If you're using the SWTOR Launcher from the SWTOR website, that's usually something like "X:\Program Files (x86)\Electronic Arts\BioWare\Star Wars - The Old Republic\Assets"
- If you're using the Steam version of SWTOR, that's usually something like "X:\Program Files (x86)\Steam\steamapps\common\Star Wars - The Old Republic\Assets"
		
X: is a variable, depending on which drive you installed SWTOR to. Your install locations may be different.
Example:
![image](https://github.com/user-attachments/assets/990a536a-c863-4266-89af-ceaac7b59eff)

	
### Output Folder:
- This is where you want the extracted files to go. Just like with Slicers GUI.
Example:
![image](https://github.com/user-attachments/assets/91f3c75c-f760-4f9b-b62c-2dce0234b9b3)
	
### -> Live Radio Button
- Tells extracTOR that you want to extract files from the "main" version of the game.
		
### -> PTS Radio Button
- Tells extracTOR that you want to extract files from the PTS version of the game.
	
### Check Hash List:
- Compares the "hashes.bin" date and the Jedipedia hash list date.
   - Prompts you to download the Jedipedia file if it is newer than the one you have.
	
### Download Hash List:
- Skips the date check and downloads the most recent Jedipedia hash list.
		
### Extract:
- Performs a file extraction.
   - Extracts the files from the .tor archives if the file exists in the hash list.
     - Using -> Live:
       Extracted files will be placed into the folder you've selected in the "Output Folder" text box.
       For example, my Live extraction is placed in: "E:\guiOut" and all the games Live files are placed in the "resources" folder inside.
![image](https://github.com/user-attachments/assets/613b7874-7cb2-4652-a2c2-45e31eb619e5)

     - Using -> PTS:
       Extracted files will be placed in a "pts" folder INSIDE the folder you've selected in the "Output Folder" text box.
       For example, my PTS extraction is placed in: "E:\guiOut" and all the games PTS files are placed in the "resources" folder inside the "pts" folder.
![image](https://github.com/user-attachments/assets/b9520001-9814-49ec-abe0-abce1430da63)

When extraction finishes a message box will pop up letting you know that extraction is complete and where you can find your extracted files.
![image](https://github.com/user-attachments/assets/febd2d97-5a72-452b-b724-77c832697f82)

## Useful Information:
- A collection of links that contains useful information, help, or tools.
