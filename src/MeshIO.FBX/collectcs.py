import os

def collect_cs_files_content(root_dir, output_file):
    """
    Recursively collects content from all .cs files in root_dir
    and writes it to output_file.

    Args:
        root_dir (str): The directory to start searching from.
        output_file (str): The path to the file where results will be saved.
    """
    if not os.path.isdir(root_dir):
        print(f"Error: Directory '{root_dir}' not found.")
        return

    with open(output_file, 'w', encoding='utf-8') as outfile:
        for dirpath, _, filenames in os.walk(root_dir):
            for filename in filenames:
                if filename.endswith(".cs"):
                    # Construct the full path to the .cs file
                    full_path = os.path.join(dirpath, filename)
                    
                    # Get the relative path from the root_dir for the header
                    # Replace OS-specific separators with '/' for consistent output
                    relative_path = os.path.relpath(full_path, root_dir).replace(os.sep, '/')

                    # Write the header
                    header = f"{relative_path}-------------\n"
                    outfile.write(header)
                    print(f"Processing: {relative_path}")

                    # Try to read the file content
                    try:
                        with open(full_path, 'r', encoding='utf-8-sig') as cs_file:
                            content = cs_file.read()
                            outfile.write(content)
                    except UnicodeDecodeError:
                        # If UTF-8 fails, try with a common fallback like latin-1 or cp1252
                        # You might need to adjust this based on your files' actual encoding
                        try:
                            with open(full_path, 'r', encoding='latin-1') as cs_file:
                                content = cs_file.read()
                                outfile.write(content)
                            outfile.write("\n[Warning: File was read using latin-1 encoding]\n")
                            print(f"Warning: {relative_path} read using latin-1 due to UTF-8 decode error.")
                        except Exception as e_fallback:
                            error_msg = f"\n[Error: Could not read file {relative_path} - {e_fallback}]\n"
                            outfile.write(error_msg)
                            print(f"Error reading {relative_path}: {e_fallback}")
                    except Exception as e:
                        error_msg = f"\n[Error: Could not process file {relative_path} - {e}]\n"
                        outfile.write(error_msg)
                        print(f"Error processing {relative_path}: {e}")

                    # Add a separator between file contents
                    outfile.write("\n\n") # Two newlines for better separation in result.txt

    print(f"\nSuccessfully collected .cs files into '{output_file}'")

# --- Configuration ---
if __name__ == "__main__":
    # Set the directory you want to scan.
    # For example, "." for the current directory where the script is run,
    # or a specific path like "C:/Projects/MyCSharpProject"
    SOURCE_DIRECTORY = "." 
    
    # Set the name for the output file
    OUTPUT_FILENAME = "result.txt"

    collect_cs_files_content(SOURCE_DIRECTORY, OUTPUT_FILENAME)