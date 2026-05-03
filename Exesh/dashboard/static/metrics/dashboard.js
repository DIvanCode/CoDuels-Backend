const kpiIds = ["startedRate", "finishedRate", "pickRate", "durationP95", "priorityP95", "workerCount"];

tick();

async function tick() {
  try {
    const response = await fetch("api/history/?minutes=30", { cache: "no-store" });
    if (!response.ok) {
      throw new Error(`request failed: ${response.status}`);
    }
    render(await response.json());
  } catch (error) {
    document.getElementById("status").innerHTML = `<span class="bad">${escapeHtml(error.message)}</span>`;
  }
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
