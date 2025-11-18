# Ten skrypt jest przestarzały. Użyj zamiast niego ./translation.sh (lub .bat na Windowsie)

import os
import sys
import re
from datetime import datetime

def extract_ftl_from_file(file_path):
    """
    Reads a single text file and extracts 'name' and 'description' values
    that are not already FTL variables.
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()

        def is_ftl_variable(s):
            if ' ' in s: return False
            if '-' not in s: return False
            return True

        found_entities = {}
        current_id = None

        line_regex = re.compile(r"(\s*)(id|name|description):\s*('|\")?(.*?)('|\")?\s*(?:#.*)?$")

        for line in lines:
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
                    current_id = None
                else:
                    current_id = entity_id
                continue

            if key in ['name', 'description']:
                if current_id:
                    if not is_ftl_variable(value):
                        found_entities.setdefault(current_id, {})
                        found_entities[current_id][key] = value

        return found_entities

    except Exception as e:
        print(f"An unexpected error occurred while processing {file_path}: {e}")
        return {}

def load_existing_ftl_ids(ftl_path):
    """Reads an existing FTL file and returns a set of all entity IDs found."""
    if not os.path.exists(ftl_path):
        return set()

    print(f"--- Reading existing FTL file: {ftl_path} ---")
    existing_ids = set()
    # Regex to find lines like: ent-SomeID = Some Name
    id_regex = re.compile(r"^\s*ent-([^=\s]+)")
    with open(ftl_path, 'r', encoding='utf-8') as f:
        for line in f:
            match = id_regex.match(line)
            if match:
                existing_ids.add(match.group(1))
    print(f"Found {len(existing_ids)} existing entities.")
    return existing_ids

def process_directory_and_generate_ftl(input_dir, output_ftl_path):
    """
    Walks a directory, finds new entities, and appends them to an FTL file.
    """
    if not os.path.isdir(input_dir):
        print(f"Error: Input path '{input_dir}' is not a valid directory.")
        return

    # 1. Load all IDs that are already in the target FTL file.
    existing_ids = load_existing_ftl_ids(output_ftl_path)

    # 2. Walk the directory and find all hardcoded entities from YAML files.
    new_data_to_append = []
    total_new_entities = 0
    print("\n--- Comparing YAML entities with existing FTL entries ---")

    for root, _, files in os.walk(input_dir):
        for filename in sorted(files):
            if filename.endswith(".yml"):
                file_path = os.path.join(root, filename)
                entities_from_file = extract_ftl_from_file(file_path)

                # Filter the found entities to get only the new ones.
                new_entities_in_file = {}
                for entity_id, data in entities_from_file.items():
                    if entity_id not in existing_ids:
                        new_entities_in_file[entity_id] = data
                        total_new_entities += 1

                if new_entities_in_file:
                    new_data_to_append.append((filename, new_entities_in_file))

    # 3. Append the new entities to the FTL file.
    if new_data_to_append:
        # Check if the file is non-empty to decide if we need a separator
        needs_separator = os.path.exists(output_ftl_path) and os.path.getsize(output_ftl_path) > 0

        with open(output_ftl_path, 'a', encoding='utf-8') as f:
            for filename, entities in new_data_to_append:
                name_written = False
                for entity_id, data in sorted(entities.items()):
                    if 'name' in data:
                        if not name_written:
                            f.write(f"\n# {filename}\n")
                            name_written = True
                        if needs_separator:
                            f.write(f"\n\n# === Entries Appended on {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} ===\n\n")
                            needs_separator = False
                        f.write(f"ent-{entity_id} = {data['name']}\n")
                        if 'description' in data and data['description']:
                            f.write(f"    .desc = {data['description']}\n")
        print(f"\nSuccessfully appended {total_new_entities} new entities to '{output_ftl_path}'.")
    else:
        print("\nNo new hardcoded strings found to add to the FTL file.")

# --- How to use the script ---
if __name__ == "__main__":
    if len(sys.argv) < 3:
        process_directory_and_generate_ftl("../../Resources/Prototypes", "..\Resources\Locale\pl-PL\prototypes\prototypes.ftl")
    elif len(sys.argv) == 3:
        input_directory = sys.argv[1]
        output_ftl_file = sys.argv[2]
        process_directory_and_generate_ftl(input_directory, output_ftl_file)
    else:
        print("Usage: python your_script_name.py <input_directory> <output_ftl_file>")
        print("Or run without arguments for a demonstration.")
