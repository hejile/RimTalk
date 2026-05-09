use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "PascalCase")]
pub struct Game {
    pub player_faction_members: Vec<Pawn>,
    pub maps: Vec<Map>,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "PascalCase")]
pub struct Map {
    pub map_name: String,
    pub items: Vec<Item>,
    pub colonist_ids: Vec<String>,
    pub animals: Vec<Pawn>,
    pub rooms: Vec<Room>,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "PascalCase")]
pub struct Item {
    pub name: String,
    pub count: i32,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "PascalCase")]
pub struct Pawn {
    pub id: String,
    pub name: String,
    pub kind: Option<String>,
    pub gender: String,
    pub age: i32,
    pub state: String,
    pub traits: Vec<String>,
    pub childhood: Option<String>,
    pub adulthood: Option<String>,
    pub mood_level: f32,
    pub top_thoughts: Vec<String>,
    pub health_status: Vec<String>,
    pub ideology: Option<String>,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "PascalCase")]
pub struct Room {
    pub role: String,
    pub cleanliness: f32,
    pub beauty: f32,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "PascalCase")]
pub struct Settings {
    pub api_key: String,
    pub provider: String,
    pub model: String,
}
