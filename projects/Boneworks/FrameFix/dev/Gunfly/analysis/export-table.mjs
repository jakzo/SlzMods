import fs from "node:fs/promises";
import { pathToFileURL } from "node:url";
export function csv(table) {
  if (
    !Array.isArray(table.headers) ||
    !Array.isArray(table.rows) ||
    table.rows.some((r) => r.length !== table.headers.length)
  )
    throw Error("Invalid verified table");
  const quote = (v) => '"' + String(v ?? "").replaceAll('"', '""') + '"';
  return (
    [table.headers, ...table.rows]
      .map((r) => r.map(quote).join(","))
      .join("\r\n") + "\r\n"
  );
}
if (
  process.argv[1] &&
  import.meta.url === pathToFileURL(process.argv[1]).href
) {
  const [input, output] = process.argv.slice(2);
  if (!output)
    throw Error("Supply verified or merged JSON and a NEW CSV path.");
  await fs.writeFile(
    output,
    csv(JSON.parse(await fs.readFile(input, "utf8"))),
    { flag: "wx" }
  );
}
