import { useState } from 'react';
import { recipeTotal, recipeToFood, recipeWeight } from '../lib/nutrition';
import { r0, r1, uid } from '../lib/util';
import { useStore } from '../store';
import type { Recipe } from '../types';
import { FoodPicker } from './food';
import { Field, Modal, Stepper } from './ui';

export function RecipeModal(props: { recipe?: Recipe; onClose: () => void }) {
  const { saveRecipe, removeRecipe } = useStore();
  const [recipe, setRecipe] = useState<Recipe>(props.recipe ?? { id: uid(), name: '', items: [] });
  const [picking, setPicking] = useState(false);

  const total = recipeTotal(recipe);
  const per = recipeToFood(recipe);
  const valid = recipe.name.trim() !== '' && recipe.items.length > 0;
  const setItem = (i: number, grams: number) =>
    setRecipe({ ...recipe, items: recipe.items.map((it, k) => (k === i ? { ...it, grams } : it)) });

  if (picking) {
    return (
      <Modal title="Ингредиент" onClose={() => setPicking(false)}>
        <FoodPicker
          onPick={(food) => {
            setRecipe({ ...recipe, items: [...recipe.items, { food, grams: food.portion ?? 100 }] });
            setPicking(false);
          }}
        />
      </Modal>
    );
  }

  return (
    <Modal title={props.recipe ? 'Блюдо' : 'Новое блюдо'} onClose={props.onClose}>
      <div className="form">
        <Field label="Название">
          <input
            className="input"
            value={recipe.name}
            onChange={(e) => setRecipe({ ...recipe, name: e.target.value })}
            placeholder="Например, омлет с сыром"
          />
        </Field>
        <Field label="Состав">
          <div className="list">
            {recipe.items.length === 0 && <div className="empty">Добавьте хотя бы один продукт</div>}
            {recipe.items.map((it, i) => (
              <div className="list-row static" key={i}>
                <div className="grow">{it.food.name}</div>
                <Stepper value={it.grams} onChange={(g) => setItem(i, g)} step={10} min={1} max={2000} unit="г" />
                <button
                  className="icon-btn"
                  title="Убрать"
                  onClick={() => setRecipe({ ...recipe, items: recipe.items.filter((_, k) => k !== i) })}
                >
                  ✕
                </button>
              </div>
            ))}
          </div>
          <button className="btn" onClick={() => setPicking(true)}>
            + Добавить продукт
          </button>
        </Field>
        {recipe.items.length > 0 && (
          <div className="notice">
            <div>
              Всё блюдо ({recipeWeight(recipe)} г): <b>{r0(total.kcal)} ккал</b> · Б {r1(total.protein)} · Ж{' '}
              {r1(total.fat)} · У {r1(total.carbs)}
            </div>
            <div>
              На 100 г: <b>{r0(per.kcal)} ккал</b> · Б {per.protein} · Ж {per.fat} · У {per.carbs}
            </div>
          </div>
        )}
        <div className="actions">
          {props.recipe && (
            <button
              className="btn danger"
              onClick={() => {
                removeRecipe(recipe.id);
                props.onClose();
              }}
            >
              Удалить
            </button>
          )}
          <button
            className="btn primary"
            disabled={!valid}
            onClick={() => {
              saveRecipe({ ...recipe, name: recipe.name.trim() });
              props.onClose();
            }}
          >
            Сохранить
          </button>
        </div>
      </div>
    </Modal>
  );
}
