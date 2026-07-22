type ChipColor = "default" | "primary" | "secondary" | "error" | "info" | "success" | "warning";

export function statusColor(status: string): ChipColor {
  switch (status) {
    case "Completed":
    case "Imported":
    case "Valid":
    case "Succeeded":
    case "RpaCompleted":
      return "success";
    case "CompletedWithErrors":
    case "Warning":
    case "RpaCompletedWithErrors":
      return "warning";
    case "Failed":
    case "Error":
      return "error";
    case "Importing":
    case "Validated":
    case "Mapped":
    case "InProgress":
    case "RpaInProgress":
      return "info";
    case "NotAttempted":
    case "Skipped":
      return "default";
    default:
      return "default";
  }
}
