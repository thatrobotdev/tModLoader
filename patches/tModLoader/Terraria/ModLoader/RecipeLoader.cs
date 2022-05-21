﻿using System;
using System.Linq;
using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader.Exceptions;

namespace Terraria.ModLoader
{
	/// <summary>
	/// This is where all Recipe and GlobalRecipe hooks are gathered and called.
	/// </summary>
	public static class RecipeLoader
	{
		internal static readonly IList<GlobalRecipe> globalRecipes = new List<GlobalRecipe>();
		internal static Recipe[] FirstRecipeForItem = new Recipe[ItemID.Count];

		/// <summary>
		/// Set when tML sets up modded recipes. Used to detect misuse of CreateRecipe
		/// </summary>
		internal static bool setupRecipes = false;

		internal static void Add(GlobalRecipe globalRecipe) {
			globalRecipes.Add(globalRecipe);
		}

		internal static void Unload() {
			globalRecipes.Clear();
			setupRecipes = false;
			FirstRecipeForItem = new Recipe[Recipe.maxRecipes];
		}

		internal static void AddRecipes() {
			foreach (Mod mod in ModLoader.Mods) {
				try {
					mod.AddRecipes();
					SystemLoader.AddRecipes(mod);

					foreach (ModItem item in mod.GetContent<ModItem>())
						item.AddRecipes();

					foreach (GlobalItem globalItem in mod.GetContent<GlobalItem>())
						globalItem.AddRecipes();
				}
				catch (Exception e) {
					e.Data["mod"] = mod.Name;
					throw;
				}
			}
		}

		internal static void PostAddRecipes() {
			foreach (Mod mod in ModLoader.Mods) {
				try {
					mod.PostAddRecipes();
					SystemLoader.PostAddRecipes(mod);
				}
				catch (Exception e) {
					e.Data["mod"] = mod.Name;
					throw;
				}
			}
		}

		internal static void PostSetupRecipes() {
			foreach (Mod mod in ModLoader.Mods) {
				try {
					SystemLoader.PostSetupRecipes(mod);
				}
				catch (Exception e) {
					e.Data["mod"] = mod.Name;
					throw;
				}
			}
		}

		/// <summary>
		/// Orders everything in the recipe according to their Ordering.
		/// </summary>
		internal static void OrderRecipes() {
			// first-pass, collect sortBefore and sortAfter
			Dictionary<Recipe, List<Recipe>> sortBefore = new();
			Dictionary<Recipe, List<Recipe>> sortAfter = new();
			var baseOrder = new List<Recipe>(Main.recipe.Length);
			foreach (var r in Main.recipe) {
				switch (r.Ordering) {
					case (null, _):
						baseOrder.Add(r);
						break;
					case (var target, false): // sortBefore
						if (!sortBefore.TryGetValue(target, out var before))
							before = sortBefore[target] = new();

						before.Add(r);
						break;
					case (var target, true): // sortBefore
						if (!sortAfter.TryGetValue(target, out var after))
							after = sortAfter[target] = new();

						after.Add(r);
						break;
				}
			}

			if (!sortBefore.Any() && !sortAfter.Any())
				return;

			// define sort function
			int i = 0;
			void Sort(Recipe r) {
				if (sortBefore.TryGetValue(r, out var before))
					foreach (var c in before)
						Sort(c);

				r.RecipeIndex = i;
				Main.recipe[i++] = r;

				if (sortAfter.TryGetValue(r, out var after))
					foreach (var c in after)
						Sort(c);
			}

			// second pass, sort!
			foreach (var r in baseOrder) {
				Sort(r);
			}

			if (i != Main.recipe.Length)
				throw new Exception("Sorting code is broken?");
		}

		/// <summary>
		/// Returns whether or not the conditions are met for this recipe to be available for the player to use.
		/// </summary>
		/// <param name="recipe">The recipe to check.</param>
		/// <returns>Whether or not the conditions are met for this recipe.</returns>
		public static bool RecipeAvailable(Recipe recipe) {
			return recipe.Conditions.All(c => c.RecipeAvailable(recipe)) && globalRecipes.All(globalRecipe => globalRecipe.RecipeAvailable(recipe));
		}

		/// <summary>
		/// Allows you to make anything happen when a player uses this recipe.
		/// </summary>
		/// <param name="item">The item crafted.</param>
		/// <param name="recipe">The recipe used to craft the item.</param>
		public static void OnCraft(Item item, Recipe recipe) {
			recipe.OnCraftHooks?.Invoke(recipe, item);

			foreach (GlobalRecipe globalRecipe in globalRecipes) {
				globalRecipe.OnCraft(item, recipe);
			}
		}

		/// <summary>
		/// Allows to edit the amount of item the player uses in a recipe.
		/// </summary>
		/// <param name="recipe">The recipe used for the craft.</param>
		/// <param name="type">Type of the ingredient.</param>
		/// <param name="amount">Modifiable amount of the item consumed.</param>
		public static void ConsumeItem(Recipe recipe, int type, ref int amount) {
			recipe.ConsumeItemHooks?.Invoke(recipe, type, ref amount);

			foreach (GlobalRecipe globalRecipe in globalRecipes) {
				globalRecipe.ConsumeItem(recipe, type, ref amount);
			}
		}
	}
}