import os
import sys
import re

def extract_ftl_from_file(file_path):
    """
    Reads a single text file and extracts 'name' and 'description' values
    that are not already FTL variables.

    Args:
        file_path (str): The path to the input text file.

    Returns:
        dict: A dictionary where keys are entity IDs and values are another
              dictionary containing the 'name' and 'description'.
    """
    print(f"--- Reading file: {file_path} ---")
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()

        # Helper function to determine if a string is likely an FTL variable
        def is_ftl_variable(s):
            if ' ' in s: return False
            if '-' not in s: return False
            return True

        # This dictionary will store the extracted data for this file
        # Format: { "entity_id": { "name": "value", "description": "value" } }
        found_entities = {}
        current_id = None
        
        # MODIFIED: Added \s*(?:#.*)? before the end anchor ($) to ignore comments.
        line_regex = re.compile(r"(\s*)(id|name|description):\s*('|\")?(.*?)('|\")?\s*(?:#.*)?$")

        for line in lines:
            # Reset current_id when a new item block starts
            if line.strip().startswith('-'):
                current_id = None
            
            match = line_regex.match(line)
            
            if not match:
                continue

            _, key, _, value, _ = match.groups()

            if key == 'id':
                entity_id = value.strip()
                if '*' in entity_id:
                    print(f"Info: Skipping entity with ID '{entity_id}' due to blacklisted '*' character.")
                    current_id = None # Invalidate this block so name/desc are ignored
                else:
                    current_id = entity_id
                continue

            if key in ['name', 'description']:
                # Only process if we have an ID for the current block
                if current_id:
                    # Only extract the value if it's a hardcoded string
                    if not is_ftl_variable(value):
                        # Ensure the entry for the ID exists
                        found_entities.setdefault(current_id, {})
                        # Store the name or description
                        found_entities[current_id][key] = value

        return found_entities

    except Exception as e:
        print(f"An unexpected error occurred while processing {file_path}: {e}")
        return {}

def process_directory_and_generate_ftl(input_dir, output_ftl_path):
    """
    Walks through a directory, processes all .yml files to extract data,
    and aggregates the results into a single FTL file using the new format.
    Source files are NOT modified.

    Args:
        input_dir (str): The path to the input directory.
        output_ftl_path (str): The path for the aggregated output FTL file.
    """
    if not os.path.isdir(input_dir):
        print(f"Error: Input path '{input_dir}' is not a valid directory.")
        return

    # This list will store tuples of (filename, entities_dict)
    # to preserve the file origin of each entity.
    data_by_file = []
    total_entities_found = 0

    # Walk through the directory tree and collect data from all .yml files
    for root, _, files in os.walk(input_dir):
        # Sort files for consistent processing order
        for filename in sorted(files):
            if filename.endswith(".yml"):
                # Print a newline to separate output for each file
                print()
                file_path = os.path.join(root, filename)
                # Extract data from the file
                entities_from_file = extract_ftl_from_file(file_path)
                
                # If any entities were found, store them with their filename
                if entities_from_file:
                    data_by_file.append((filename, entities_from_file))

    # Write the aggregated FTL file if any entries were created
    if data_by_file:
        with open(output_ftl_path, 'w', encoding='utf-8') as f:
            # Iterate through the list of (filename, entities) tuples
            for filename, entities in data_by_file:
                # Write the filename as a comment
                f.write(f"# {filename}\n")
                
                # Write the entities from this file, sorted by ID
                for entity_id, data in sorted(entities.items()):
                    # An entity must have a name to be included
                    if 'name' in data:
                        # Write the main entity name line
                        f.write(f"ent-{entity_id} = {data['name']}\n")
                        total_entities_found += 1
                        
                        # If there is also a description, write it as an attribute
                        if 'description' in data and data['description']:
                            f.write(f"    .desc = {data['description']}\n")
                f.write(f"\n")

        print(f"\nSuccessfully generated aggregated FTL file: '{output_ftl_path}'.")
    else:
        print("\nNo hardcoded strings found that required replacement. FTL file not created.")
        
    print(f"Total entities with hardcoded names found and processed: {total_entities_found}")


# --- How to use the script ---
if __name__ == "__main__":
    if len(sys.argv) < 3:
        process_directory_and_generate_ftl("..\Resources\Prototypes", "..\Resources\Locale\pl-PL\prototypes\prototypes.ftl")
    elif len(sys.argv) == 3:
        input_directory = sys.argv[1]
        output_ftl_file = sys.argv[2]
        process_directory_and_generate_ftl(input_directory, output_ftl_file)
    else:
        print("Usage: python translationTool.py <input_directory> <output_ftl_file>")
