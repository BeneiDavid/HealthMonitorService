import pandas as pd
import sys

SEPARATOR = ";"

def main():
    if len(sys.argv) != 4:
        print("Usage:")
        print("python add_runid.py input.csv output.csv run_id")
        sys.exit(1)

    input_file = sys.argv[1]
    output_file = sys.argv[2]
    run_id_value = sys.argv[3]

    # Read WITH header
    df = pd.read_csv(input_file, sep=SEPARATOR)

    # Insert run_id column as first column
    df.insert(0, "run_id", run_id_value)

    if "failure" in df.columns:
        df["failure"] = df["failure"].replace(-1, 0)
    else:
        print("Error: 'failure' column not found.")
        sys.exit(1)

    df.to_csv(output_file, sep=SEPARATOR, index=False)

    print(f"Rows processed: {len(df)}")
    print(f"Added column: run_id = {run_id_value}")
    print("Values of -1 in 'failure' column replaced with 0")
    print(f"Saved to: {output_file}")


if __name__ == "__main__":
    main()