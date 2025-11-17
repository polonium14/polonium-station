import os
import sys
import re
from ruamel.yaml import YAML
from datetime import datetime

def extract_ftl_from_file(file_path):
    """
    Reads a single text file and extracts 'name' and 'description' values
    that are not already FTL variables.
    """
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            with YAML() as yaml:
                document = yaml.load(f)

            entities = {data['id']: data for data in document if 'type' in data and data['type'] == 'entity' and 'id' in data and data['id']}

            valid_entities = {id: entities[id] for id in entities if 'description' in entities[id] or 'name' in entities[id] }
            return valid_entities

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

                if new_entities_in_file:
                    new_data_to_append.append((filename, new_entities_in_file))

    # 3. Append the new entities to the FTL file.
    if new_data_to_append:
        # Check if the file is non-empty to decide if we need a separator
        needs_separator = os.path.exists(output_ftl_path) and os.path.getsize(output_ftl_path) > 0

        with open(output_ftl_path, 'a', encoding='utf-8') as f:
            if new_data_to_append and needs_separator:
                f.write(f"\n\n# === Entries Appended on {datetime.now().strftime('%Y-%m-%d %H:%M:%S')} ===\n\n")
            for filename, entities in new_data_to_append:
                f.write(f"\n# {filename}\n")
                for entity_id, data in sorted(entities.items()):
                    if 'name' not in data and 'description' not in data:
                        continue

                    f.write(f"ent-{entity_id} =")
                    if 'name'  in data:
                        f.write(f" {data['name']}")
                    f.write("\n")
                    if 'description' in data and data['description']:
                        f.write(f"    .desc = {data['description']}\n")
            print(f"\nSuccessfully appended {len(new_data_to_append)} new entities to '{output_ftl_path}'.")
    else:
        print("\nNo new hardcoded strings found to add to the FTL file.")

# --- How to use the script ---
if __name__ == "__main__":
    if len(sys.argv) < 3:
        process_directory_and_generate_ftl("../../Resources/Prototypes", "../../Resources/Locale/pl-PL/prototypes/prototypes.ftl")
    elif len(sys.argv) == 3:
        input_directory = sys.argv[1]
        output_ftl_file = sys.argv[2]
        process_directory_and_generate_ftl(input_directory, output_ftl_file)
    else:
        print("Usage: python your_script_name.py <input_directory> <output_ftl_file>")
        print("Or run without arguments for a demonstration.")
