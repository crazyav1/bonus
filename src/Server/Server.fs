module Server

open System.Runtime.Caching
open System
open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open SAFE
open Saturn
open Shared

let cache = MemoryCache.Default

let dataUrl = "https://dashboard.elering.ee/api"

let parsePowerSystemData (jsonArray: JsonElement) =
    jsonArray.EnumerateArray()
    |> Seq.map (fun item -> {
        timestamp = item.GetProperty("timestamp").GetInt32()
        production = if item.GetProperty("production").ValueKind = JsonValueKind.Null then None else Some(item.GetProperty("production").GetDouble())
        consumption = if item.GetProperty("consumption").ValueKind = JsonValueKind.Null then None else Some(item.GetProperty("consumption").GetDouble())
        losses = if item.GetProperty("losses").ValueKind = JsonValueKind.Null then None else Some(item.GetProperty("losses").GetDouble())
        frequency = item.GetProperty("frequency").GetDouble()
        system_balance = if item.GetProperty("system_balance").ValueKind = JsonValueKind.Null then None else Some(item.GetProperty("system_balance").GetDouble())
        ac_balance = if item.GetProperty("ac_balance").ValueKind = JsonValueKind.Null then None else Some(item.GetProperty("ac_balance").GetDouble())
        production_renewable = if item.GetProperty("production_renewable").ValueKind = JsonValueKind.Null then None else Some(item.GetProperty("production_renewable").GetDouble())
        solar_energy_production = if item.GetProperty("solar_energy_production").ValueKind = JsonValueKind.Null then None else Some(item.GetProperty("solar_energy_production").GetDouble())
    })
    |> List.ofSeq


let eleringApi ctx = {
    getDayAheadPriceData = fun (request: Request) -> async {
        printfn "Request: %A" request
        let url = $"{dataUrl}/nps/price?start={request.startDate}T20%%3A59%%3A59.999Z&end={request.endDate}T20%%3A59%%3A59.999Z"
        printfn "URL: %s" url
        use client = new HttpClient()
        let! response = client.GetStringAsync(url) |> Async.AwaitTask
        let json = JsonDocument.Parse(response).RootElement

        let parsePriceData (jsonArray: JsonElement) : PriceData list =
            jsonArray.EnumerateArray()
            |> Seq.map (fun item -> { timestamp = item.GetProperty("timestamp").GetInt32(); price = item.GetProperty("price").GetDouble() })
            |> List.ofSeq

        return Response.create (json.GetProperty("success").GetBoolean(), {
            ee = parsePriceData (json.GetProperty("data").GetProperty("ee"))
            lv = parsePriceData (json.GetProperty("data").GetProperty("lv"))
            lt = parsePriceData (json.GetProperty("data").GetProperty("lt"))
            fi = parsePriceData (json.GetProperty("data").GetProperty("fi"))
        })
    }

    getPowerSystemData= fun (request: Request) -> async {
        printfn "Request: %A" request
        let url = $"{dataUrl}/system?start={request.startDate}T20%%3A59%%3A59.999Z&end={request.endDate}T20%%3A59%%3A59.999Z"
        printfn "URL: %s" url
        use client = new HttpClient()
        let! response = client.GetStringAsync(url) |> Async.AwaitTask
        let json = JsonDocument.Parse(response).RootElement

        let powerSystemData = parsePowerSystemData (json.GetProperty("data"))
        return PowerSystemResponse.create (powerSystemData, json.GetProperty("success").GetBoolean())
        }

    getCurrentPriceData = fun () -> async {
        try
            let cacheKey = "currentPriceData"
            match cache.Get(cacheKey) with
            | :? CurrentPriceResponse as cachedData -> return { cachedData with cached = true }
            | _ ->
                let url = $"{dataUrl}/nps/price/EE/current"
                printfn "URL: %s" url
                use client = new HttpClient()
                let! response = client.GetStringAsync(url) |> Async.AwaitTask
                let json = JsonDocument.Parse(response).RootElement
                let parseCurrentPriceData (jsonArray: JsonElement): PriceData list =
                    jsonArray.EnumerateArray()
                    |> Seq.map (fun item -> { timestamp = item.GetProperty("timestamp").GetInt32(); price = item.GetProperty("price").GetDouble() })
                    |> List.ofSeq
                let result = CurrentPriceResponse.create (parseCurrentPriceData (json.GetProperty("data")), false)

                let cacheItemPolicy = CacheItemPolicy()
                cacheItemPolicy.AbsoluteExpiration <- DateTimeOffset.Now.AddMinutes(1.0)
                cache.Set(cacheKey, result, cacheItemPolicy)

                return result
        with
        | ex ->
            printfn "An error occurred: %s" ex.Message
            return CurrentPriceResponse.create ([], false)
    }
}

let webApp = Api.make eleringApi

let app = application {
    use_router webApp
    memory_cache
    use_static "public"
    use_gzip
}

[<EntryPoint>]
let main _ =
    run app
    0