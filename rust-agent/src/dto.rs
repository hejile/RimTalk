use serde::{Deserialize, Serialize};

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct Game {
    #[serde(rename = "PlayerFactionMembers")]
    pub player_faction_members: Vec<Pawn>,
    #[serde(rename = "Maps")]
    pub maps: Vec<Map>,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct Map {
    #[serde(rename = "MapName")]
    pub map_name: String,
    #[serde(rename = "Items")]
    pub items: Vec<Item>,
    #[serde(rename = "ColonistIds")]
    pub colonist_ids: Vec<String>,
    #[serde(rename = "Animals")]
    pub animals: Vec<Pawn>,
    #[serde(rename = "Rooms")]
    pub rooms: Vec<Room>,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct Item {
    #[serde(rename = "Name")]
    pub name: String,
    #[serde(rename = "Count")]
    pub count: i32,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct Pawn {
    #[serde(rename = "Id")]
    pub id: String,
    #[serde(rename = "Name")]
    pub name: String,
    #[serde(rename = "Kind")]
    pub kind: Option<String>,
    #[serde(rename = "Gender")]
    pub gender: String,
    #[serde(rename = "Age")]
    pub age: i32,
    #[serde(rename = "State")]
    pub state: String,
    #[serde(rename = "Traits")]
    pub traits: Vec<String>,
    #[serde(rename = "Childhood")]
    pub childhood: Option<String>,
    #[serde(rename = "Adulthood")]
    pub adulthood: Option<String>,
    #[serde(rename = "MoodLevel")]
    pub mood_level: f32,
    #[serde(rename = "TopThoughts")]
    pub top_thoughts: Vec<String>,
    #[serde(rename = "HealthStatus")]
    pub health_status: Vec<String>,
    #[serde(rename = "Ideology")]
    pub ideology: Option<String>,
}

#[derive(Debug, Serialize, Deserialize, Default)]
#[serde(rename_all = "camelCase")]
pub struct Room {
    #[serde(rename = "Role")]
    pub role: String,
    #[serde(rename = "Cleanliness")]
    pub cleanliness: f32,
    #[serde(rename = "Beauty")]
    pub beauty: f32,
}
