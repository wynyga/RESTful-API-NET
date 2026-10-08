"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";

import { ApiError, api, isDemo } from "@/lib/api";
import { Button, Card, Spinner, TextField } from "./ui";

const messages: Record<number, string> = {
  404: "That city is unknown to the weather provider.",
  429: "Too many lookups. Wait a moment and try again.",
  502: "The weather provider answered badly or could not be reached.",
  504: "The weather provider is too slow right now.",
};

/** A lookup that goes through the API to a third-party service; the failures are mapped to distinct statuses. */
export default function WeatherCard() {
  const [input, setInput] = useState("Manado");
  const [city, setCity] = useState("Manado");

  const weather = useQuery({
    queryKey: ["weather", city.toLowerCase()],
    queryFn: () => api.weather(city),
    retry: false,
    staleTime: 10 * 60_000,
    enabled: city.length > 0,
  });

  const err = weather.error instanceof ApiError ? weather.error : null;

  return (
    <Card className="p-5">
      <h2 className="text-base font-semibold">Weather</h2>
      <p className="mt-1 text-sm text-muted">Site conditions, fetched through the API from a third-party service.</p>

      <form
        className="mt-4 flex items-end gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          setCity(input.trim());
        }}
      >
        <div className="flex-1">
          <TextField label="City" value={input} onChange={(e) => setInput(e.target.value)} />
        </div>
        <Button type="submit" variant="primary" className="mb-0.5">
          Look up
        </Button>
      </form>

      <div className="mt-4 min-h-24" aria-live="polite">
        {weather.isFetching ? (
          <p className="flex items-center gap-2 text-sm text-muted">
            <Spinner className="size-3.5" /> Looking up {city}…
          </p>
        ) : weather.isError ? (
          <div role="alert" className="rounded-lg bg-danger-soft px-3 py-2 text-sm text-danger">
            <p className="font-medium">
              {err ? `${err.status} · ` : ""}
              {err ? (messages[err.status] ?? "The lookup failed.") : "The lookup failed."}
            </p>
            {err && <p className="mt-0.5 opacity-90">{err.message}</p>}
          </div>
        ) : weather.data ? (
          <div className="flex items-end gap-4">
            <p className="text-5xl font-semibold tabular-nums">{weather.data.temperature}°</p>
            <div className="pb-1 text-sm">
              <p className="font-medium">{weather.data.city}</p>
              <p className="text-muted">
                {weather.data.description} · humidity {weather.data.humidity}%
              </p>
            </div>
          </div>
        ) : null}
      </div>

      {isDemo() && (
        <p className="mt-2 text-xs text-muted">
          Demo: try <button type="button" className="underline" onClick={() => { setInput("Atlantis"); setCity("Atlantis"); }}>Atlantis</button> (404),{" "}
          <button type="button" className="underline" onClick={() => { setInput("Slowville"); setCity("Slowville"); }}>Slowville</button> (504) or{" "}
          <button type="button" className="underline" onClick={() => { setInput("Glitch"); setCity("Glitch"); }}>Glitch</button> (502) to see each failure mapped.
        </p>
      )}
    </Card>
  );
}
