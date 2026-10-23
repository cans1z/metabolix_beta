import { useState } from 'react';
import { BarcodeModal, FoodCardModal, FoodFormModal, FoodPicker } from '../components/food';
import { RecipeModal } from '../components/RecipeModal';
import { Icon } from '../components/icons';
import { useStore } from '../store';
import type { Food, Recipe } from '../types';

export function Foods() {
  const { state, logout } = useStore();
  const [card, setCard] = useState<Food | null>(null);
  const [scan, setScan] = useState(false);
  const [form, setForm] = useState<{ barcode?: string } | null>(null);
  const [recipe, setRecipe] = useState<{ value?: Recipe } | null>(null);
  const authed = !!state.user;

  return (
    <div className="page">
      {!authed && (
        <div className="notice">
          Вы смотрите справочник без входа. Дневник и свои продукты доступны после авторизации.{' '}
          <button className="link" onClick={logout}>
            Войти
          </button>
        </div>
      )}
      <header className="page-head">
        <h1>Продукты</h1>
        <div className="toolbar">
          <button className="btn" onClick={() => setScan(true)}>
            <Icon name="barcode" size={18} />
            Штрихкод
          </button>
          {authed && (
            <>
              <button className="btn" onClick={() => setRecipe({})}>
                <Icon name="plus" size={16} />
                Блюдо
              </button>
              <button className="btn primary" onClick={() => setForm({})}>
                <Icon name="plus" size={16} />
                Продукт
              </button>
            </>
          )}
        </div>
      </header>
      <div className="card">
        <FoodPicker
          onPick={setCard}
          onEditRecipe={authed ? (id) => setRecipe({ value: state.recipes.find((r) => r.id === id) }) : undefined}
        />
      </div>

      {card && <FoodCardModal food={card} onClose={() => setCard(null)} />}
      {scan && (
        <BarcodeModal
          onClose={() => setScan(false)}
          onFound={(f) => {
            setScan(false);
            setCard(f);
          }}
          onCreate={(barcode) => {
            setScan(false);
            if (authed) setForm({ barcode });
          }}
        />
      )}
      {form && <FoodFormModal barcode={form.barcode} onClose={() => setForm(null)} onSaved={setCard} />}
      {recipe && <RecipeModal recipe={recipe.value} onClose={() => setRecipe(null)} />}
    </div>
  );
}
