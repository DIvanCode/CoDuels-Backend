const kpiIds = ["startedRate", "finishedRate", "pickRate", "durationP95", "priorityP95", "workerCount"];

initializeTimeRangeInputs();
tick();

async function tick() {
  const url = apiHistoryUrl();
  if (!url) {
    document.getElementById("status").textContent = "Select start and end time.";
    return;
  }
  try {
    const response = await fetch(url, { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`request failed: ${response.status}`);
    }
    render(await response.json());
  } catch (error) {
    document.getElementById("status").innerHTML = `<span class="bad">${escapeHtml(error.message)}</span>`;
  }
}

function apiHistoryUrl() {
  const params = new URLSearchParams(window.location.search);
  if (!params.get("start") || !params.get("end")) {
    return "";
  }
  return `api/history/?${params.toString()}`;
}

function initializeTimeRangeInputs() {
  const params = new URLSearchParams(window.location.search);
  document.getElementById("startInput").value = params.get("start") || "";
  document.getElementById("endInput").value = params.get("end") || "";
}

function render(payload) {
  document.getElementById("status").innerHTML = payload.status || "No data.";
  for (const id of kpiIds) {
    document.getElementById(id).textContent = payload.kpis?.[id] || "0";
  }
  setHtml("throughputChart", payload.charts?.throughput);
  setHtml("fairnessChart", payload.charts?.fairness);
  setHtml("executionPriorityChart", payload.charts?.execution_priority);
  setHtml("executionProgressChart", payload.charts?.execution_progress);
  setHtml("slotChart", payload.charts?.worker_slots);
  setHtml("memoryChart", payload.charts?.worker_memory);
  setHtml("rectangles", payload.charts?.rectangles);
  setHtml("workerPool", payload.tables?.worker_pool);
  setHtml("workers", payload.tables?.workers);
}

function setHtml(id, html) {
  document.getElementById(id).innerHTML = html || "";
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
