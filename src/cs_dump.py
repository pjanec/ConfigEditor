import os
import sys
import re

# Define the file extensions and folders to include/exclude.
# This makes it easy to configure the script's behavior.
ALLOWED_EXTENSIONS = ('.cs', '.xaml', '.csproj', '.sln', '.json')
EXCLUDED_FOLDERS = ('bin', 'obj')


def get_unique_output_filename(base_path):
    """
    Generates a unique filename by finding the highest existing numeric postfix
    and incrementing it. Always returns a filename with a numeric postfix (e.g., 'output_1.txt').

    For a base_path 'C:\\Dumps\\output.txt', if 'output_1.txt' and 'output_3.txt' exist,
    this function will return 'C:\\Dumps\\output_4.txt'.
    """
    directory = os.path.dirname(base_path)
    base_filename, ext = os.path.splitext(os.path.basename(base_path))

    # ---- FIX: Only try to create a directory if the path is not empty. ----
    if directory and not os.path.exists(directory):
        os.makedirs(directory)

    # Use the current directory if the directory part is empty
    list_dir = directory if directory else '.'

    highest_num = 0
    # Regex to match filenames like 'base_1.txt', 'base_12.txt', etc.
    pattern = re.compile(f"^{re.escape(base_filename)}_(\\d+){re.escape(ext)}$")

    for filename in os.listdir(list_dir):
        match = pattern.match(filename)
        if match:
            num = int(match.group(1))
            if num > highest_num:
                highest_num = num

    # The new file will have the next number in sequence.
    new_number = highest_num + 1
    new_filename = f"{base_filename}_{new_number}{ext}"

    return os.path.join(directory, new_filename)


def collect_source_files(input_dir, output_file):
    """
    Walks through a directory, collecting specified source files, and writes them
    to a single output file.

    Args:
        input_dir (str): The root directory to start searching from.
        output_file (str): The path to the output file to be created.
    """
    unique_output_file = get_unique_output_filename(output_file)

    with open(unique_output_file, 'w', encoding='utf-8') as outfile:
        # os.walk allows us to modify the 'dirs' list in-place to prune the search.
        for root, dirs, files in os.walk(input_dir):
            # Exclude specified folders (case-insensitive) and hidden folders.
            dirs[:] = [
                d for d in dirs
                if d.lower() not in EXCLUDED_FOLDERS and not d.startswith('.')
            ]

            # Sort files to ensure a consistent order
            files.sort()
            for filename in files:
                # Check if the file has one of the allowed extensions.
                if filename.lower().endswith(ALLOWED_EXTENSIONS):
                    file_path = os.path.join(root, filename)
                    rel_path = os.path.relpath(file_path, input_dir)

                    # Write a generic, language-agnostic separator.
                    outfile.write('\n\n')
                    outfile.write(f"{'-'*20} File: {rel_path} {'-'*20}\n")
                    outfile.write('-' * (48 + len(rel_path)) + '\n\n')

                    # Write the file content, handling potential read errors.
                    try:
                        with open(file_path, 'r', encoding='utf-8') as infile:
                            outfile.write(infile.read())
                    except Exception as e:
                        outfile.write(f"Error reading file: {e}\n")

    print(f"Done. Output written to {unique_output_file}")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        print("Usage: python cs_dump.py <input_folder> <output_file>")
        print("Example: python cs_dump.py C:\\Projects\\MyWpfApp C:\\Dumps\\output.txt")
    else:
        input_folder = sys.argv[1]
        output_file_name = sys.argv[2]
        
        if not os.path.isdir(input_folder):
            print(f"Error: Input folder '{input_folder}' not found.")
            sys.exit(1)

        collect_source_files(input_folder, output_file_name)