namespace Shared

open System

type Request = {
    startDate: string
    endDate: string
}

type PriceData = {
    timestamp: int
    price: float
}

type Data = {
    ee: PriceData list
    lv: PriceData list
    lt: PriceData list
    fi: PriceData list
}

type Response = {
    success: bool
    data: Data
}

type CurrentPriceResponse = {
    priceData: PriceData list
    cached : bool
}

module CurrentPriceResponse =
    let create (currentPriceData: PriceData list, cached: bool) = {
        priceData = currentPriceData
        cached = cached
    }

module Response =
    let create (success: bool, data: Data) = {
        success = success
        data = data
    }

type PowerSystemData = {
    timestamp: int
    production: float option
    consumption: float option
    losses: float option
    frequency: float
    system_balance: float option
    ac_balance: float option
    production_renewable: float option
    solar_energy_production: float option
}

type PowerSystemResponse = {
    data: PowerSystemData list
    success: bool
}

module PowerSystemResponse =
    let create (data: PowerSystemData list, success: bool) = {
        data = data
        success = success
    }

module Request =
    let isValid (startDate: string, endDate: string) =
        String.IsNullOrWhiteSpace startDate ||  String.IsNullOrWhiteSpace endDate |> not

    let create (startDate: string, endDate: string) = {
        startDate = startDate
        endDate = endDate
    }

type IEleringApi = {
    getDayAheadPriceData: Request -> Async<Response>
    getPowerSystemData: Request -> Async<PowerSystemResponse>
    getCurrentPriceData: unit -> Async<CurrentPriceResponse>
}