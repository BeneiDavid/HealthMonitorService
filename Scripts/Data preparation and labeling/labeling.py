import sys
import pandas as pd

# CONFIGURATION

COLS = [
    "run_id", "timestamp", "monitoring_starttime", "operating_system", "device_ip", "machine_name", 
    "cpu_usage_percent", "cpu_queue_length", "load_per_core", "memory_usage_percent", "secondary_memory_usage_percent",
    "io_wait_percent", "disk_usage_percent", "disk_latency_ms", "process_count", "network_available", "packet_loss_percent", "failure"
]

# Thresholds selected after inspecting the stressed Windows/Linux runs.
# They mark degraded resource states, not only confirmed failures.
THRESHOLDS = {
    "cpu": 95,
    "memory": 95,
    "combined_cpu": 90,
    "combined_memory": 80,
    "io": 25,
    "disk_latency": 20,
    "queue": 15,
    "gap": 8
}

MISSING_VALUE_COLS = [
    "cpu_queue_length",
    "load_per_core",
    "secondary_memory_usage_percent",
    "io_wait_percent",
    "packet_loss_percent"
]

# METHODS

# Set shorter format for dates
def format_timestamp(x):
    if pd.isnull(x):
        return ""
    return x.strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"

# Check if 2 consecutive measurements kept pressure
def is_sustained(cond, window=2):
    cond = cond.fillna(False).astype(int)
    consecutive = cond.rolling(window=window, min_periods=window).sum()
    return consecutive >= window

# Apply labeling logic for normal states
def apply_labels(df):
    is_win = df["operating_system"].str.contains("windows", case=False, na=False)
    is_lin = df["operating_system"].str.contains("linux|industrial", case=False, na=False)

    gap = (
        df.groupby(["run_id", "machine_name"])["timestamp"].diff().dt.total_seconds()
    )

    cpu_stress = df["cpu_usage_percent"] >= THRESHOLDS["cpu"]
    mem_stress = df["memory_usage_percent"] >= THRESHOLDS["memory"]
    combined_stress = ((df["cpu_usage_percent"] >= THRESHOLDS["combined_cpu"]) & (df["memory_usage_percent"] >= THRESHOLDS["combined_memory"]))
    queue_stress = is_win & (df["cpu_queue_length"] >= THRESHOLDS["queue"])
    io_stress = is_lin & (df["io_wait_percent"] >= THRESHOLDS["io"])

    # Multi-metric score-based evaluation
    mod_score = (
        (df["cpu_usage_percent"] >= 85).fillna(False).astype(int) +
        (df["memory_usage_percent"] >= 75).fillna(False).astype(int) +
        (df["disk_latency_ms"] >= THRESHOLDS["disk_latency"]).fillna(False).astype(int) +
        (is_win & (df["cpu_queue_length"] >= 10)).fillna(False).astype(int)
    )

    # Calculate risk
    is_risky = (
        (is_sustained(cpu_stress) & is_sustained(queue_stress | io_stress)) |
        is_sustained(combined_stress) |
        is_sustained(mem_stress) |
        is_sustained(mod_score >= 2) |
        (df["network_available"] == 0) |
        (df["packet_loss_percent"] >= 1.0) |
        (gap >= THRESHOLDS["gap"])
    )

    # Preserve manually set labels, only change normal states
    df.loc[(df["failure"] == 0) & is_risky, "failure"] = 1

    return df

# MAIN

def main(input_path, output_path):
    df = pd.read_csv(
        input_path,
        sep=";",
        names=COLS,
        header=0,
        parse_dates=["timestamp", "monitoring_starttime"]
    )

    numeric_cols = [
        "cpu_usage_percent",
        "cpu_queue_length",
        "load_per_core",
        "memory_usage_percent",
        "secondary_memory_usage_percent",
        "io_wait_percent",
        "disk_usage_percent",
        "disk_latency_ms",
        "process_count",
        "network_available",
        "packet_loss_percent",
        "failure"
    ]

    for col in numeric_cols:
        df[col] = pd.to_numeric(df[col], errors="coerce")

    for col in MISSING_VALUE_COLS:
        df[col] = df[col].replace(-1, pd.NA)

    df = df.dropna(subset=["timestamp", "monitoring_starttime"])
    df = apply_labels(df)

    for col in ["timestamp", "monitoring_starttime"]:
        df[col] = df[col].apply(format_timestamp)

    df.to_csv(output_path, sep=";", index=False, na_rep="")
    counts = df["failure"].value_counts().sort_index()

    print("Labeling finished")
    print("Label distribution:")

    for label, count in counts.items():
        print(f"{int(label)}: {count}")


if __name__ == "__main__":
    if len(sys.argv) < 3:
        exit("Usage: py labeling.py input.csv output.csv")

    main(sys.argv[1], sys.argv[2])