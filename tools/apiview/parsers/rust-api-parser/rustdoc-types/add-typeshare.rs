use std::fs::{File, self};
use std::io::{self, Read, Write};
use std::path::Path;

const INPUT_PATH: &str = "vendor/rustdoc-types/src/lib.rs";
const OUTPUT_PATH: &str = "output/lib-typeshare.rs";

fn main() -> io::Result<()> {
    let input_path = Path::new(INPUT_PATH);
    let output_path = Path::new(OUTPUT_PATH);

    // Open the input file and read its content into a string
    let mut input_file = File::open(&input_path)?;
    let mut content = String::new();
    input_file.read_to_string(&mut content)?;

    // Transform the content by adding typeshare annotations
    let transformed_content = add_typeshare_annotations(&content);

    // Create the directory path recursively if it doesn't exist
    if let Some(parent) = output_path.parent() {
        fs::create_dir_all(parent)?;
    }

    // Write the transformed content to the output file
    let mut output_file = File::create(&output_path)?;
    output_file.write_all(transformed_content.as_bytes())?;

    Ok(())
}

fn add_typeshare_annotations(content: &str) -> String {
    let mut result = String::new();
    let mut in_pub_item = false;
    let mut added_use_statement = false;
    let mut brace_count = 0;

    // Iterate over each line of the input content
    for line in content.lines() {
        // Add the use statement for typeshare if the line is not a comment
        if !added_use_statement && !line.trim_start().starts_with("//!") {
            result.push_str("use typeshare::typeshare;\n");
            added_use_statement = true;
        }

        // Check if the line starts a public item and is not a module
        if line.trim_start().starts_with("pub ") && brace_count == 0 {
            in_pub_item = true;
            // Add #[serde(skip)] for the Span struct because we do not uise it and since tuples are not supported by #[typeshare]
            if line.contains("pub struct Span {") {
                result.push_str("#[serde(skip)]\n");
            } else {
                // Add #[typeshare] for all public items except the FxHashMap type alias
                if !line
                    .trim_start()
                    .contains("pub type FxHashMap<K, V> = HashMap<K, V>;")
                {
                    result.push_str("#[typeshare]\n");
                }
            }
        }

        // Add #[serde(tag = "type", content = "content")] because #[typeshare] forces us to!
        if line.trim_start().starts_with("pub enum") {
            let enum_name = line.split_whitespace().nth(2).unwrap_or("");
            if enum_name != "ItemKind"
                && enum_name != "TraitBoundModifier"
                && enum_name != "MacroKind"
            {
                result.push_str("#[serde(tag = \"type_typeshare\", content = \"content_typeshare\")]\n");
                // Need to handle this in post-processing to eventually get "type": { content }
            }
        }

        // Add #[serde(skip)] for the fields of the Span struct, because we skip the Span struct
        if line.trim_start().contains("pub span: Option<Span>,") 
        // Add #[serde(skip)] for the inputs field because #[typeshare] does not support tuples
        // Need to handle this in post-processing
            || line.trim_start().contains("pub inputs: Vec<(String, Type)>,")
        {
            result.push_str("#[serde(skip)]\n");
        }

        // Track the nesting level of braces to determine the end of public items
        if in_pub_item {
            brace_count += line.chars().filter(|&c| c == '{').count();
            brace_count -= line.chars().filter(|&c| c == '}').count();

            if brace_count == 0 {
                in_pub_item = false;
            }
        }

        // Add the current line to the result
        result.push_str(line);
        result.push('\n');
    }

    result
}
